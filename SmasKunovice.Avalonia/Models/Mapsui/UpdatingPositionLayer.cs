using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;
using SmasKunovice.Avalonia.Models.Dronetag;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer : UpdatingLayer<PointFeature>
{
    private readonly IAircraftDatabase _aircraftDatabase;
    private readonly TargetStyleBuilder _targetStyleBuilder;
    private const string SelectedFeatureField = "selected";
    private const string StaleFeatureField = "stale";
    private IFeature? _currentSelectedFeature;
    private const int StaleThresholdSeconds = 5;
    private const int InactiveThresholdSeconds = 60;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(StaleThresholdSeconds));
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly RpaPresenceConflictDetector _rpaPresenceConflictDetector;
    private readonly RunwayApproachConflictDetector _runwayApproachConflictDetector;
    private readonly DroneGridIntersectionDetector _droneGridIntersectionDetector;
    private readonly Brush _normalLabelColor = new(Color.WhiteSmoke);
    private readonly Brush _warningLabelColor = new(new Color(255, 222, 40));
    private readonly Brush _alarmLabelColor = new(new Color(255, 0, 0));
    private readonly Map _map;
    private bool _disposed;
    private readonly Task _updateLoopTask;
    private readonly List<ConflictFeature> _activeConflicts = [];
    private readonly List<string> _noConflictCandidates = [];

    public event EventHandler<IFeature?>? SelectedFeatureChanged;
    public event EventHandler<ConflictFeature>? NewConflict;
    public event EventHandler<string>? ResolvedConflictsForAircraft;

    public UpdatingPositionLayer(IProvider dataSource,
        IAircraftDatabase aircraftDatabase,
        TargetStyleBuilder targetStyleBuilder,
        Map map, DroneGridIntersectionDetector droneGridIntersectionDetector,
        RpaPresenceConflictDetector rpaPresenceConflictDetector,
        RunwayApproachConflictDetector runwayApproachConflictDetector, IErrorDialogService errorDialogService) : base(dataSource, errorDialogService)
    {
        _aircraftDatabase = aircraftDatabase;
        _targetStyleBuilder = targetStyleBuilder;
        _map = map;
        _map.Info += OnMapOnInfo;
        _droneGridIntersectionDetector = droneGridIntersectionDetector;
        _rpaPresenceConflictDetector = rpaPresenceConflictDetector;
        _runwayApproachConflictDetector = runwayApproachConflictDetector;
        IsMapInfoLayer = true;
        Style = CreateStyle();
        _updateLoopTask = Task.Run(() => RunUpdateLoopAsync(_cts.Token)); // Task.Run prevents the task to run on UI thread
    }

    private void OnMapOnInfo(object? sender, MapInfoEventArgs e) => ToggleSelected(e.MapInfo?.Feature);

    private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                await UpdateDataAsync(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Error updating position layer");
            Dispose();
            await _errorDialogService.ShowErrorDialogAsync("Error updating position layer", e);
        }
    }

    private void ToggleSelected(IFeature? feature)
    {
        if (feature is null) return;

        // Deselect previous feature if it exists
        if (_currentSelectedFeature is not null && _currentSelectedFeature != feature)
        {
            _currentSelectedFeature[SelectedFeatureField] = null;
        }

        // Toggle selection on current feature
        if (feature[SelectedFeatureField] is null)
        {
            feature[SelectedFeatureField] = "true";
            _currentSelectedFeature = feature;
        }
        else
        {
            feature[SelectedFeatureField] = null;
            _currentSelectedFeature = null;
        }

        SelectedFeatureChanged?.Invoke(this, _currentSelectedFeature);
    }

    private ThemeStyle CreateStyle()
    {
        return new ThemeStyle(f =>
        {
            var scoutData = f.GetScoutData() ?? throw new InvalidOperationException("Feature has no ScoutData");
            _targetStyleBuilder.Initialize(scoutData);
            if (f[StaleFeatureField] is true)
                _targetStyleBuilder.WithStale();

            var appliedStyle = _targetStyleBuilder.Build();
            if (f[SelectedFeatureField]?.ToString() == "true")
            {
                return new StyleCollection
                {
                    Styles =
                    {
                        _targetStyleBuilder.Initialize(scoutData).WithSelected().Build(),
                        appliedStyle
                    }
                };
            }

            return appliedStyle;
        });
    }

    protected override async Task ProcessFeaturesAsync(IEnumerable<PointFeature> updateFeatures, bool reprocessing)
    {
        var semaphoreAcquired = false;
        try
        {
            await _semaphore.WaitAsync(_cts.Token);
            semaphoreAcquired = true;
            var utcNow = DateTime.UtcNow;
            foreach (var updatedFeature in updateFeatures)
            {
                ProcessFeatureUpdates(reprocessing, updatedFeature, utcNow);
            }

            if (reprocessing)
            {
                ProcessConflictDetectors();
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        finally
        {
            if (semaphoreAcquired)
                _semaphore.Release();
        }
    }

    private void ProcessFeatureUpdates(bool reprocessing, PointFeature updatedFeature, DateTime utcNow)
    {
        var id = updatedFeature.GetScoutDataId();
        PointFeature featureToProcess;
        if (Features.TryGetValue(id, out var existingFeature))
        {
            if (IsFeatureInactive(existingFeature, utcNow))
            {
                RemoveFeature(id);
                return;
            }

            existingFeature.Point.X = updatedFeature.Point.X;
            existingFeature.Point.Y = updatedFeature.Point.Y;
            existingFeature[ScoutData.FeatureScoutDataField] = updatedFeature.GetScoutData();
            SetStaleFeature(existingFeature, utcNow); // Grey out stale features
            featureToProcess = existingFeature;
        }
        else
        {
            Features[id] = updatedFeature;
            featureToProcess = updatedFeature;
        }

        EnsureLabelStyle(featureToProcess);

        if (!reprocessing) // If we are just processing new features, we don't have the whole context, so can't determine conflicts
            return;

        if (_droneGridIntersectionDetector.IsDroneAboveLimit(featureToProcess))
        {
            LogExtensions.LogInfo("Found drone height limit conflict for feature ID: {0}, level: {1}", this, featureToProcess.GetScoutDataId(), ConflictLevel.Alarm);
            var conflictFeature = new ConflictFeature(featureToProcess, ConflictLevel.Alarm, "Drone above limit");
            _activeConflicts.Add(conflictFeature);
            NewConflict?.Invoke(this, conflictFeature);
            SetLabelColor(featureToProcess, ConflictLevel.Alarm);
        }
        else if (!_rpaPresenceConflictDetector.ProcessConflictCandidate(featureToProcess) && !_runwayApproachConflictDetector.ProcessConflictCandidate(featureToProcess))
            _noConflictCandidates.Add(id);
    }

    private void ProcessConflictDetectors()
    {
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

    private void SetLabelColor(PointFeature featureToProcess, ConflictLevel conflictLevel)
    {
        var labelStyle = featureToProcess.Styles.OfType<LabelStyle>().Single();
        labelStyle.BackColor = conflictLevel switch
        {
            ConflictLevel.None => _normalLabelColor,
            ConflictLevel.Warning => _warningLabelColor,
            ConflictLevel.Alarm => _alarmLabelColor,
            _ => throw new ArgumentOutOfRangeException(nameof(conflictLevel), conflictLevel, null)
        };
    }

    private static bool IsFeatureInactive(PointFeature existingFeature, DateTime utcNow)
    {
        var updateTimeStamp = existingFeature.GetScoutData()?.Odid.Location?.GetTimestamp();
        if (updateTimeStamp is null) // if the feature doesn't have a timestamp, we will update it to stale later
            return false;

        return utcNow - updateTimeStamp > TimeSpan.FromSeconds(InactiveThresholdSeconds);
    }

    private static void SetStaleFeature(PointFeature feature, DateTime utcNow)
    {
        var updateTimeStamp = feature.GetScoutData()?.Odid.Location?.GetTimestamp();
        if (updateTimeStamp is null)
        {
            feature[StaleFeatureField] = true; // if the feature doesn't have a timestamp, we consider it stale
            return;
        }

        var isStale = utcNow - updateTimeStamp > TimeSpan.FromSeconds(StaleThresholdSeconds);
        feature[StaleFeatureField] = isStale;
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }

    private void EnsureLabelStyle(PointFeature feature)
    {
        var style = GetLabelStyle(feature);
        if (style is null)
        {
            style = new LabelStyle
            {
                BackColor = _normalLabelColor,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                Offset = new RelativeOffset(0, .9),
                Opacity = 0.3f, // doesn't seem to work
                Font = new Font() { Size = 9, FontFamily = "Arial" },
            };
            feature.Styles.Add(style);
        }

        style.Text = GetDisplayText(feature);
    }

    private static LabelStyle? GetLabelStyle(PointFeature feature)
    {
        foreach (var style in feature.Styles)
        {
            if (style is LabelStyle labelStyle)
                return labelStyle;
        }

        return null;
    }

    private string GetDisplayText(PointFeature feature)
    {
        var scoutData = feature.GetScoutData();
        if (scoutData is null) return "???";
        string registration;
        if (feature[MainViewViewModel.IdOverrideFeatureAttribute] is string overrideId)
        {
            registration = overrideId;
        }
        else
        {
            var uasId = scoutData.GetUasId();
            var aircraftRecord = _aircraftDatabase.GetByIcao24(uasId);
            registration = aircraftRecord?.Registration ?? uasId;
        }

        var heightString = AircraftDataFormatter.GetHeightString(scoutData);
        var speedString = AircraftDataFormatter.GetSpeedString(scoutData);

        var displayText = $"{registration} \n" +
                          $"{heightString} " +
                          $"{speedString}";

        return displayText;
    }

    public void SetLabelVisibility(bool visible)
    {
        if (_currentSelectedFeature is null) return;
        var style = _currentSelectedFeature.Styles.OfType<LabelStyle>().FirstOrDefault();
        if (style != null) style.Enabled = visible;
        OnDataChanged(new DataChangedEventArgs(Name));
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _cts.Cancel();
            _timer.Dispose();
            try
            {
                _updateLoopTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _cts.Dispose();
            _semaphore.Dispose();
            _map.Info -= OnMapOnInfo;
        }

        base.Dispose(disposing);
        _disposed = true;
    }

    public sealed override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}