using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using SmasKunovice.Avalonia.Extensions;
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
    private readonly IntersectionDetector _droneGridIntersectionDetector;
    private readonly RpaPresenceConflictDetector _rpaPresenceConflictDetector;
    private readonly Brush _normalLabelColor = new(Color.WhiteSmoke);
    private readonly Brush _warningLabelColor = new(new Color(255, 222, 40));
    private readonly Brush _alarmLabelColor = new(new Color(255, 0, 0));
    private readonly RunwayApproachConflictDetector _runwayApproachConflictDetector;
    private readonly Map _map;
    private bool _disposed;

    public event EventHandler<IFeature?>? SelectedFeatureChanged;

    public UpdatingPositionLayer(IProvider dataSource,
        IAircraftDatabase aircraftDatabase,
        TargetStyleBuilder targetStyleBuilder,
        Map map, IntersectionDetector droneGridIntersectionDetector,
        RpaPresenceConflictDetector rpaPresenceConflictDetector,
        RunwayApproachConflictDetector runwayApproachConflictDetector) : base(dataSource)
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
        Task.Run(async () =>
        {
            try
            {
                await RunUpdateLoopAsync(_cts.Token);

            }
            catch (Exception e)
            {
                LogExtensions.LogError(e, "Error updating position layer");
            }
        });
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
        catch (OperationCanceledException)
        {
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
        await _semaphore.WaitAsync();
        try
        {
            var utcNow = DateTime.UtcNow;
            foreach (var updatedFeature in updateFeatures)
            {
                var id = updatedFeature.GetScoutDataId();
                PointFeature featureToProcess;
                if (Features.TryGetValue(id, out var existingFeature))
                {
                    if (IsFeatureInactive(existingFeature, utcNow))
                    {
                        RemoveFeature(id);
                        continue;
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
                    continue;

                var conflictLevel = ConflictLevel.None;
                _rpaPresenceConflictDetector.ProcessConflictCandidate(featureToProcess);
                _runwayApproachConflictDetector.ProcessConflictCandidate(featureToProcess);
                if (IsDroneAboveLimit(featureToProcess))
                {
                    conflictLevel = ConflictLevel.Alarm;
                    LogExtensions.LogInfo("Found drone height limit conflict for feature ID: {0}, level: {1}", this, featureToProcess.GetScoutDataId(), conflictLevel);
                }
                
                SetLabelColor(featureToProcess, conflictLevel);
            }

            if (reprocessing)
            {
                ProcessConflictDetectors();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ProcessConflictDetectors()
    {
        foreach (var conflictFeature in _rpaPresenceConflictDetector.GetConflictFeatures())
        {
            SetLabelColor(conflictFeature.Feature, conflictFeature.ConflictLevel);
            LogExtensions.LogInfo("Found conflict in RPA for feature ID: {0}, level: {1}", this, conflictFeature.Feature.GetScoutDataId(), conflictFeature.ConflictLevel);
        }

        foreach (var conflictFeature in _runwayApproachConflictDetector.GetConflictFeatures(_rpaPresenceConflictDetector))
        {
            SetLabelColor(conflictFeature.Feature, conflictFeature.ConflictLevel);
            LogExtensions.LogInfo("Found conflict in runway approach for feature ID: {0}, level: {1}", this, conflictFeature.Feature.GetScoutDataId(), conflictFeature.ConflictLevel);
        }

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

    private bool IsDroneAboveLimit(PointFeature feature)
    {
        var scoutData = feature.GetScoutData();

        if (scoutData?.IsDrone() is not true)
            return false;

        if (!TryGetIntersectingVerticalLimit(feature, out var verticalLimit))
            return false;

        var altitudeMeters = feature.GetScoutData()?.Odid.Location?.AltitudeBaro;
        return altitudeMeters >= verticalLimit; // Drone is dangerous when it's high up
    }

    private static bool IsFeatureInactive(PointFeature existingFeature, DateTime utcNow)
    {
        var updateTimeStamp = existingFeature.GetScoutData()?.GetTimestamp();
        if (updateTimeStamp is null) // if the feature doesn't have a timestamp, we will update it to stale later
            return false;

        return utcNow - updateTimeStamp > TimeSpan.FromSeconds(InactiveThresholdSeconds);
    }

    private static void SetStaleFeature(PointFeature feature, DateTime utcNow)
    {
        var updateTimeStamp = feature.GetScoutData()?.GetTimestamp();
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

    private bool TryGetIntersectingVerticalLimit(PointFeature feature, out int? verticalLimit)
    {
        verticalLimit = null;
        if (_droneGridIntersectionDetector.TryGetIntersectFeature(feature, out var candidate) is not true)
            return false;

        var verticalLimitString = candidate!["vertical_limit"]?.ToString();
        if (verticalLimitString is null)
        {
            LogExtensions.LogError("Vertical limit not found on grid feature. Feature ID: {0}.", this,
                feature.GetScoutDataId());
            return false;
        }
        // GND - <num> m AGL
        var dashIndex = verticalLimitString.IndexOf('-');
        var mIndex = verticalLimitString.IndexOf('m');

        if (dashIndex == -1 || mIndex == -1 || mIndex <= dashIndex)
        {
            LogExtensions.LogError("Vertical limit string is not in expected format. Feature ID: {0}.", this,
                feature.GetScoutDataId());
            return false;
        }

        var valueSpan = verticalLimitString.AsSpan(dashIndex + 1, mIndex - dashIndex - 1).Trim();
        var parseResult = int.TryParse(valueSpan, out var verticalLimitValue);
        
        if (!parseResult)
        {
            LogExtensions.LogError("Failed to extract vertical limit value from string: {0}.", this, valueSpan.ToString());
            return false;
        }

        verticalLimit = verticalLimitValue;
        return true;
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