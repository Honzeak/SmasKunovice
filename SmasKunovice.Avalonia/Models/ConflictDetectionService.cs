using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class ConflictDetectionService(DynamicScoutDataProvider scoutDataProvider, IErrorDialogService dialogService, DroneGridIntersectionDetector droneDetector, RpaPresenceConflictDetector rpaDetector, RunwayApproachConflictDetector approachDetector) : IDisposable
{
    private const int UpdateIntervalSeconds = 2;
    private bool _disposed;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(UpdateIntervalSeconds));
    private readonly ConflictRepository _conflictRepository = new();
    private readonly ConcurrentDictionary<string, PointFeature> _features = new();
    private readonly CancellationTokenSource _cts = new();

    public async Task InitializeAsync()
    {
        await scoutDataProvider.ConnectClientAsync(); // TODO this may not be required, UpdateLayer doesn't have this
        scoutDataProvider.DataChanged += OnProviderDataChanged;
        _ = Task.Run(() => RunPeriodicProcessing(_cts.Token));
    }

    private async Task RunPeriodicProcessing(CancellationToken token)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(token))
            {
                UpdateConflictsAsync(); // TODO should I pass token here?
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Error updating position layer", this);
            Dispose();
            await dialogService.ShowErrorDialogAsync("Error updating conflicts. Shutting down conflict detection.", e); // TODO the dialog service could notify other users about the failure so the whole app can shut down.
        }
    }

    private void UpdateConflictsAsync()
    {
        foreach (var kvp in _features)
        {
            var featureToProcess = kvp.Value;
            var featureId = kvp.Key;
            
            rpaDetector.ProcessConflictCandidate(featureToProcess);
            approachDetector.ProcessConflictCandidate(featureToProcess);

            if (droneDetector.IsDroneAboveLimit(featureToProcess))
            {
                LogExtensions.LogInfo("Found drone height limit conflict for feature ID: {0}, level: {1}", this, featureId, ConflictLevel.Alarm);
                var conflictFeature = new ConflictFeature(featureToProcess, ConflictLevel.Alarm, "Drone above limit");
                _conflictRepository.AddConflict(featureId, ConflictType.DroneAboveLimit, conflictFeature);
                // NewConflict?.Invoke(this, conflictFeature);
                // SetLabelColor(featureToProcess, ConflictLevel.Alarm); TODO I think I should notify after finished processing loop, to have info about all detected conflicts, and raise for highest conflict level.
            }
        }

        else if (!_rpaPresenceConflictDetector.ProcessConflictCandidate(featureToProcess) && !_runwayApproachConflictDetector.ProcessConflictCandidate(featureToProcess))
            _noConflictCandidates.Add(id);

        // Runway approach detector additionally sets conflict for RPA candidates as well, so needs to be processed first.
        foreach (var conflictFeature in _runwayApproachConflictDetector.GetConflictFeatures(_rpaPresenceConflictDetector))
        {
            if (!_activeConflicts.Contains(conflictFeature))
            {
                _activeConflicts.Add(conflictFeature);
                SetLabelColor(conflictFeature.Feature, conflictFeature.ConflictLevel);
                if (conflictFeature.ConflictLevel is ConflictLevel.None)
                    NewConflict?.Invoke(this, conflictFeature);
            }

            LogExtensions.LogInfo("Found conflict in runway approach for feature ID: {0}, level: {1}", this, conflictFeature.Feature.GetScoutDataId(), conflictFeature.ConflictLevel);
        }

        foreach (var conflictFeature in _rpaPresenceConflictDetector.GetConflictFeatures())
        {
            if (!_activeConflicts.Contains(conflictFeature))
            {
                _activeConflicts.Add(conflictFeature);
                SetLabelColor(conflictFeature.Feature, conflictFeature.ConflictLevel);
                if (conflictFeature.ConflictLevel is not ConflictLevel.None)
                    NewConflict?.Invoke(this, conflictFeature);
            }

            LogExtensions.LogInfo("Found conflict in RPA for feature ID: {0}, level: {1}", this, conflictFeature.Feature.GetScoutDataId(), conflictFeature.ConflictLevel);
        }

        foreach (var noConflictCandidateId in _noConflictCandidates)
        {
            var toRemove = _activeConflicts.Where(cf => cf.Feature.GetScoutDataId() == noConflictCandidateId);
            _activeConflicts.RemoveMany(toRemove);
            ResolvedConflictsForAircraft?.Invoke(this, noConflictCandidateId);
            SetLabelColor(Features[noConflictCandidateId], ConflictLevel.None);
        }

        _noConflictCandidates.Clear();
        _rpaPresenceConflictDetector.Reset();
        _runwayApproachConflictDetector.Reset();
    }

    public void RemoveFeature(string featureId)
    {
        var result = _features.TryRemove(featureId, out _);
        if (result)
            _conflictRepository.RemoveById(featureId);
        else
            LogExtensions.LogWarning("Feature with id {0} not found in active context.", this, featureId);
    }

    private async void OnProviderDataChanged(object? sender, EventArgs e)
    {
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

    private async Task UpdateFeaturesFromProvider()
    {
        foreach (var feature in await scoutDataProvider.GetFeaturesAsync())
        {
            var pointFeature = (PointFeature)feature;
            _features.AddOrUpdate(pointFeature.GetScoutDataId(), pointFeature, (_, _) => pointFeature);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        scoutDataProvider.DataChanged -= OnProviderDataChanged;
        _disposed = true;
    }
}