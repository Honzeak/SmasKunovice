using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public interface IConflictDetectionService : IDisposable
{
    event EventHandler<ConflictsUpdateEventArgs>? ConflictUpdate;
    Task InitializeAsync();
    void RemoveFeature(string featureId);
    void SetRunwayOperation(RunwayDirection direction, bool value);
}

public class ConflictDetectionService(DynamicScoutDataProvider scoutDataProvider, IErrorDialogService dialogService, DroneAboveLimitConflictDetector droneDetector, RpaPresenceConflictDetector rpaDetector, (RunwayApproachConflictDetector _02C, RunwayApproachConflictDetector _20C) approachDetectors) : IConflictDetectionService
{
    private const int UpdateIntervalSeconds = 3;
    private const int TakeoffThresholdMps = 11; // ~40 km/h TODO check if we agree on this value
    private const int TakeoffHeadingOffsetDegrees = 60;
    private bool _disposed;
    private readonly ConflictRepository _conflictRepository = new();
    private readonly ConcurrentDictionary<string, PointFeature> _rpaConflictZoneFeatures = new();
    private readonly ConcurrentDictionary<string, PointFeature> _approachConflictZoneFeatures02C = new();
    private readonly ConcurrentDictionary<string, PointFeature> _approachConflictZoneFeatures20C = new();
    private readonly ConcurrentDictionary<string, PointFeature> _droneConflictZoneFeatures = new();
    private bool _isInitialized;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(UpdateIntervalSeconds));
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _02C;
    private bool _20C;
    private readonly HeadingRangeEvaluator _headingRangeEvaluator02C = new(20, TakeoffHeadingOffsetDegrees);
    private readonly HeadingRangeEvaluator _headingRangeEvaluator20C = new(200, TakeoffHeadingOffsetDegrees);

    public event EventHandler<ConflictsUpdateEventArgs>? ConflictUpdate;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await scoutDataProvider.ConnectClientAsync();
        scoutDataProvider.DataChanged += OnProviderDataChanged;
        _ = Task.Run(PeriodicConflictUpdateAsync);
    }

    public void SetRunwayOperation(RunwayDirection direction, bool value)
    {
        LogExtensions.LogDebug($"Runway operation set to {value} for direction {direction}");
        
        switch (direction)
        {
            case RunwayDirection._20C:
                _20C = value;
                if (!_20C)
                    UpdateConflictsAndRaiseEvent(_approachConflictZoneFeatures20C, ConflictType.RunwayApproach, ConflictLevel.None);
                break;
            case RunwayDirection._02C:
                _02C = value;
                if (!_02C)
                    UpdateConflictsAndRaiseEvent(_approachConflictZoneFeatures02C, ConflictType.RunwayApproach, ConflictLevel.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        ProcessApproachConflicts();
    }

    private async void OnProviderDataChanged(object? sender, EventArgs eventArgs)
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
        {
        }
        catch (Exception exception)
        {
            LogExtensions.LogError(exception, $"Error updating layer in {nameof(ConflictDetectionService)}. Service exiting.", this);
            await dialogService.ShowErrorDialogAsync("Error updating layer. Service exiting.", exception);
            Dispose();
        }
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

    private void ProcessApproachConflicts()
    {
        var maxConflictLevel = ConflictLevel.None;
        if (_rpaConflictZoneFeatures.IsEmpty || _rpaConflictZoneFeatures.All(IsTakeoffFeature))
        {
            LogExtensions.LogDebug("No valid features in RPA for approach conflict. No conflict raised!");
            UpdateConflictsAndRaiseEvent(_approachConflictZoneFeatures02C, ConflictType.RunwayApproach, ConflictLevel.None);
            UpdateConflictsAndRaiseEvent(_approachConflictZoneFeatures20C, ConflictType.RunwayApproach, ConflictLevel.None);
            return;
        }

        if (_02C)
        {
            LogExtensions.LogDebug($"Processing approach conflicts for runway 02C ({_approachConflictZoneFeatures02C.Count} features)");
            foreach (var (uasId, feature) in _approachConflictZoneFeatures02C)
            {
                var conflictLevel = approachDetectors._02C.GetConflictLevel(feature);
                UpdateConflictAndRaiseEvent(feature, ConflictType.RunwayApproach, conflictLevel);
                maxConflictLevel = conflictLevel > maxConflictLevel ? conflictLevel : maxConflictLevel;
            }
        }

        if (_20C)
        {
            LogExtensions.LogDebug($"Processing approach conflicts for runway 20C ({_approachConflictZoneFeatures20C.Count} features)");
            foreach (var (uasId, feature) in _approachConflictZoneFeatures20C)
            {
                var conflictLevel = approachDetectors._20C.GetConflictLevel(feature);
                UpdateConflictAndRaiseEvent(feature, ConflictType.RunwayApproach, conflictLevel);
                maxConflictLevel = conflictLevel > maxConflictLevel ? conflictLevel : maxConflictLevel;
            }
        }
        
        LogExtensions.LogDebug($"Max conflict level for approach features: {maxConflictLevel}");
        UpdateConflictsAndRaiseEvent(_rpaConflictZoneFeatures, ConflictType.RunwayApproach, maxConflictLevel);
    }

    private void ProcessRpaConflicts()
    {
        var conflictLevel = ConflictLevel.None;

        // TODO check if this implementation is correct
        // TODO not atomical comparison!
        if (_rpaConflictZoneFeatures.Count >= 2 &&
            _rpaConflictZoneFeatures.Any(kvp => !kvp.Value.GetScoutData().IsVehicle()))
        {
            if (_rpaConflictZoneFeatures.Count > 2)
            {
                conflictLevel = ConflictLevel.Alarm;
            }
            else
            {
                var firstFeature = _rpaConflictZoneFeatures.First().Value;
                var secondFeature = _rpaConflictZoneFeatures.Last().Value;

                conflictLevel = IsFeatureDistanceIncreasing(firstFeature, secondFeature)
                    ? ConflictLevel.None
                    : ConflictLevel.Alarm;
            }
        }

        foreach (var (uasId, feature) in _rpaConflictZoneFeatures)
        {
            UpdateConflictAndRaiseEvent(feature, ConflictType.RpaPresence, conflictLevel);
        }
    }

    private static bool IsFeatureDistanceIncreasing(PointFeature pointA, PointFeature pointB)
    {
        if (pointA[FeatureAttributes.PreviousPosition] is MPoint pointAPrev && pointB[FeatureAttributes.PreviousPosition] is MPoint pointBPrev)
            return CalculateDistance(pointAPrev, pointBPrev) < CalculateDistance(pointA.Point, pointB.Point);

        LogExtensions.LogWarning("Unable to determine previous positions when resolving RPA conflicts");
        return false;

        float CalculateDistance(MPoint p0, MPoint p1)
        {
            var deltaX = p1.X - p0.X;
            var deltaY = p1.Y - p0.Y;

            return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }
    }

    private async Task UpdateFeaturesFromProvider()
    {
        // Updated features from message
        foreach (var feature in await scoutDataProvider.GetFeaturesAsync())
        {
            var pointFeature = (PointFeature)feature;
            var scoutDataId = pointFeature.GetScoutDataId();

            if (approachDetectors._02C.IsInConflictZone(pointFeature))
                _approachConflictZoneFeatures02C[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.RunwayApproach, RunwayDirection._02C);


            if (approachDetectors._20C.IsInConflictZone(pointFeature))
                _approachConflictZoneFeatures20C[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.RunwayApproach, RunwayDirection._20C);


            if (rpaDetector.IsInConflictZone(pointFeature))
                _rpaConflictZoneFeatures[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.RpaPresence, null);

            if (droneDetector.IsInConflictZone(pointFeature))
                _droneConflictZoneFeatures[scoutDataId] = pointFeature;
            else
                RemoveFromZoneAndConflictRepoAndRaiseEvent(scoutDataId, pointFeature, ConflictType.DroneAboveLimit, null);
        }
    }

    private bool IsTakeoffFeature(KeyValuePair<string, PointFeature> arg)
    {
        var pointFeature = arg.Value;
        var scoutData = pointFeature.GetScoutData();
        var speed = scoutData.Odid.Location?.SpeedHorizontal;
        var heading = scoutData.Odid.Location?.Direction;

        if (scoutData.Odid.Location?.IsGrounded is true)
            return false;

        if (speed < TakeoffThresholdMps || heading is null)
            return false;

        if (_20C && _headingRangeEvaluator20C.IsWithinBounds(heading.Value) || _02C && _headingRangeEvaluator02C.IsWithinBounds(heading.Value))
        {
            LogExtensions.LogDebug("Found a takeoff feature!");
            return true;
        }

        return false;
    }


    private void RemoveFromZoneAndConflictRepoAndRaiseEvent(string uasId, PointFeature pointFeature, ConflictType conflictType, RunwayDirection? direction)
    {
        var removed = conflictType switch
        {
            ConflictType.DroneAboveLimit => _droneConflictZoneFeatures.Remove(uasId, out _),
            ConflictType.RpaPresence => _rpaConflictZoneFeatures.Remove(uasId, out _),
            ConflictType.RunwayApproach when direction == RunwayDirection._02C => _approachConflictZoneFeatures02C.Remove(uasId, out _),
            ConflictType.RunwayApproach when direction == RunwayDirection._20C => _approachConflictZoneFeatures20C.Remove(uasId, out _),
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
        _approachConflictZoneFeatures02C.Remove(featureId, out _);
        _approachConflictZoneFeatures20C.Remove(featureId, out _);
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