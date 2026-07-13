using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public interface IConflictDetectionService : IDisposable
{
    event EventHandler<ConflictsUpdateEventArgs>? ConflictUpdate;
    Task InitializeAsync();
    void RemoveFeature(string featureId);
}

public class ConflictDetectionService(DynamicScoutDataProvider scoutDataProvider, IErrorDialogService dialogService, DroneAboveLimitConflictDetector droneDetector, RpaPresenceConflictDetector rpaDetector, RunwayApproachConflictDetector approachDetector) : IConflictDetectionService
{
    private const long UpdateIntervalSeconds = 3;
    private bool _disposed;
    private readonly ConflictRepository _conflictRepository = new();
    private readonly ConcurrentDictionary<string, PointFeature> _rpaConflictZoneFeatures = new();
    private readonly ConcurrentDictionary<string, PointFeature> _approachConflictZoneFeatures = new();
    private readonly ConcurrentDictionary<string, PointFeature> _droneConflictZoneFeatures = new();
    private bool _isInitialized;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(UpdateIntervalSeconds));
    private readonly CancellationTokenSource _cts = new();
    private SemaphoreSlim _semaphore = new(1);

    public event EventHandler<ConflictsUpdateEventArgs>? ConflictUpdate;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await scoutDataProvider.ConnectClientAsync(); // TODO this may not be required, UpdateLayer doesn't have this
        scoutDataProvider.DataChanged += OnProviderDataChanged;
        _ = Task.Run(PeriodicConflictUpdateAsync);
    }

    private async void OnProviderDataChanged(object? sender, EventArgs e)
    {
        try
        {
            await _semaphore.WaitAsync(_cts.Token);
            try
            {
                await UpdateFeaturesFromProvider();
            }
            catch (Exception exception)
            {
                LogExtensions.LogError(exception, "Error updating layer");
                Dispose();
                await dialogService.ShowErrorDialogAsync("Error updating layer", exception);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        { }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task PeriodicConflictUpdateAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                UpdateConflicts();
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, $"Error updating conflicts in {nameof(ConflictDetectionService)}. Service exiting.", this);
            await dialogService.ShowErrorDialogAsync("Error updating conflicts. Service exiting.", e);
            Dispose();
        }
    }

    private void UpdateConflicts()
    {
        ProcessApproachConflicts();
        ProcessRpaConflicts();

        foreach (var (uasId, feature) in _droneConflictZoneFeatures)
        {
            UpdateConflictAndRaiseEvent(feature, ConflictType.DroneAboveLimit, ConflictLevel.Alarm);
        }
    }

    private void ProcessRpaConflicts()
    {
        var conflictLevel = ConflictLevel.None;
        if (!_rpaConflictZoneFeatures.IsEmpty
            && _rpaConflictZoneFeatures.Count >= 2
            && _rpaConflictZoneFeatures.Any(kvp => !kvp.Value.GetScoutData().IsVehicle()))
        {
            conflictLevel = ConflictLevel.Alarm;
        }
        
        foreach (var (uasId, feature) in _rpaConflictZoneFeatures)
        {
            UpdateConflictAndRaiseEvent(feature, ConflictType.RpaPresence, conflictLevel);
        }
    }

    private async Task UpdateFeaturesFromProvider()
    {
        // Updated features from message
        foreach (var feature in await scoutDataProvider.GetFeaturesAsync())
        {
            var pointFeature = (PointFeature)feature;
            var scoutDataId = pointFeature.GetScoutDataId();

            if (approachDetector.IsInConflictZone(pointFeature))
                _approachConflictZoneFeatures[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.RunwayApproach);

            if (rpaDetector.IsInConflictZone(pointFeature))
                _rpaConflictZoneFeatures[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.RpaPresence);

            if (droneDetector.IsInConflictZone(pointFeature))
                _droneConflictZoneFeatures[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.DroneAboveLimit);
        }
    }

    private void ProcessApproachConflicts()
    {
        var maxConflictLevel = ConflictLevel.None;
        if (_rpaConflictZoneFeatures.IsEmpty)
        {
            UpdateConflictsAndRaiseEvent(_approachConflictZoneFeatures, ConflictType.RunwayApproach, ConflictLevel.None);
            return;
        }

        foreach (var (uasId, feature) in _approachConflictZoneFeatures)
        {
            var conflictLevel = approachDetector.GetConflictLevel(feature);
            UpdateConflictAndRaiseEvent(feature, ConflictType.RunwayApproach, conflictLevel);
            maxConflictLevel = conflictLevel > maxConflictLevel ? conflictLevel : maxConflictLevel;
        }

        UpdateConflictsAndRaiseEvent(_rpaConflictZoneFeatures, ConflictType.RunwayApproach, maxConflictLevel);
    }


    private void RemoveFromZoneAndConflictRepoAndRaiseEvent(string uasId, PointFeature pointFeature, ConflictType conflictType)
    {
        var removed = conflictType switch
        {
            ConflictType.DroneAboveLimit => _droneConflictZoneFeatures.Remove(uasId, out _),
            ConflictType.RpaPresence => _rpaConflictZoneFeatures.Remove(uasId, out _),
            ConflictType.RunwayApproach => _approachConflictZoneFeatures.Remove(uasId, out _),
            _ => throw new ArgumentOutOfRangeException(nameof(conflictType), conflictType, null)
        };
        if (!removed)
            return;
        
        _conflictRepository.RemoveConflict(uasId, conflictType);
        LogExtensions.LogDebug("Remove conflict event raised", this);
        ConflictUpdate?.Invoke(this, new ConflictsUpdateEventArgs(pointFeature, ConflictUpdateResult.Removed, conflictType, ConflictLevel.None));
    }

    private void UpdateConflictAndRaiseEvent(PointFeature pointFeature, ConflictType conflictType, ConflictLevel conflictLevel)
    {
        var updateResult = _conflictRepository.UpdateConflict(pointFeature.GetScoutDataId(), conflictType, conflictLevel);
        if (updateResult == ConflictUpdateResult.Unchanged)
            return;

        LogExtensions.LogDebug("Update conflict event raised", this);
        ConflictUpdate?.Invoke(this, new ConflictsUpdateEventArgs(pointFeature, updateResult, conflictType, conflictLevel));
    }

    private void UpdateConflictsAndRaiseEvent(ConcurrentDictionary<string, PointFeature> conflictZoneFeatures, ConflictType conflictType, ConflictLevel conflictLevel)
    {
        var updateResults = _conflictRepository.UpdateConflicts(conflictZoneFeatures.Keys, conflictType, conflictLevel);
        if (!updateResults.HasChanges)
            return;

        LogExtensions.LogDebug("Update conflicts event raised", this);
        ConflictUpdate?.Invoke(this, new ConflictsUpdateEventArgs(updateResults, conflictZoneFeatures.ToDictionary(), conflictType, conflictLevel));
    }

    public void RemoveFeature(string featureId)
    {
        _approachConflictZoneFeatures.Remove(featureId, out _);
        _rpaConflictZoneFeatures.Remove(featureId, out _);
        _droneConflictZoneFeatures.Remove(featureId, out _);
        _conflictRepository.RemoveById(featureId);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _timer.Dispose();

        scoutDataProvider.DataChanged -= OnProviderDataChanged;
        _disposed = true;
    }
}