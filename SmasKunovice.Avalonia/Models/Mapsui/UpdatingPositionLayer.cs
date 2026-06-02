using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.RegularExpressions;
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
    private readonly IAircraftSymbolProvider _aircraftSymbolProvider;
    private const string SelectedFeatureField = "selected";
    private const string StaleFeatureField = "stale";
    private const string DroneAboveLimitFeatureField = "droneAboveLimit";
    private IFeature? _currentSelectedFeature;
    private const int StaleThresholdSeconds = 5;
    private const int InactiveThresholdSeconds = 60;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Regex _verticalLimitValueRegex = new(@"GND - (?<metersValue>\d+) m AGL", RegexOptions.Compiled);
    private readonly IntersectionDetector _droneGridIntersectionDetector;
    private readonly RpaPresenceConflictDetector _rpaPresenceConflictDetector;
    private readonly Brush _normalLabelColor = new(Color.WhiteSmoke);
    private readonly Brush _warningLabelColor = new(new Color(255, 222, 40));
    private readonly Brush _alarmLabelColor = new(new Color(255, 0, 0));
    private readonly RunwayApproachConflictDetector _runwayApproachConflictDetector;

    public event EventHandler<IFeature?>? SelectedFeatureChanged;

    public UpdatingPositionLayer(IProvider dataSource,
        IAircraftDatabase aircraftDatabase,
        IAircraftSymbolProvider aircraftSymbolProvider,
        Map map, IntersectionDetector droneGridIntersectionDetector,
        RpaPresenceConflictDetector rpaPresenceConflictDetector,
        RunwayApproachConflictDetector runwayApproachConflictDetector) : base(dataSource)
    {
        _aircraftDatabase = aircraftDatabase;
        _aircraftSymbolProvider = aircraftSymbolProvider;
        map.Info += (sender, e) => ToggleSelected(e.MapInfo?.Feature);
        _droneGridIntersectionDetector = droneGridIntersectionDetector;
        _rpaPresenceConflictDetector = rpaPresenceConflictDetector;
        _runwayApproachConflictDetector = runwayApproachConflictDetector;
        IsMapInfoLayer = true;
        Style = CreateStyle();
        _timer = new Timer(_ => UpdateDataAsync(false).GetAwaiter().GetResult(), null,
            TimeSpan.FromSeconds(StaleThresholdSeconds), Timeout.InfiniteTimeSpan);
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
            var state = SymbolState.Default;
            if (f[DroneAboveLimitFeatureField] is true) // If a drone is below limit but is stale, we prefer to display the stale state
                state = SymbolState.DroneAboveLimit;
            if (f[StaleFeatureField] is true)
                state = SymbolState.Stale;

            if (f[SelectedFeatureField]?.ToString() == "true")
            {
                return new StyleCollection
                {
                    Styles =
                    {
                        CreateAircraftSymbol(f, SymbolState.Selected),
                        CreateAircraftSymbol(f, state)
                    }
                };
            }

            return CreateAircraftSymbol(f, state);
        });
    }

    private IStyle CreateAircraftSymbol(IFeature feature, SymbolState selectedStyle)
    {
        var scoutData = feature.GetScoutData();
        return scoutData?.Tech switch
        {
            "B4" or "B5" or "WN" or "WB" => _aircraftSymbolProvider.GetDroneStyle(selectedStyle),
            _ => _aircraftSymbolProvider.GetAirplaneStyle(selectedStyle)
        };
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

                SetDroneAboveLimit(featureToProcess);
                SetLabelStyle(featureToProcess);
                if (!reprocessing) // If we are just processing new features, we don't have the whole context, so can't determine conflicts
                    continue;
                
                var processRpaResult = _rpaPresenceConflictDetector.ProcessConflictCandidate(featureToProcess);
                var processApproachResult = _runwayApproachConflictDetector.ProcessConflictCandidate(featureToProcess);
                if (!processRpaResult && !processApproachResult)
                    SetLabelColor(featureToProcess, ConflictLevel.None);
            }

            if (reprocessing) // a non-reprocessing call shouldn't restart the timer, since we need to touch all features at least every N seconds
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

                RestartTimer();
            }
        }
        finally
        {
            _semaphore.Release();
        }
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

    private void SetDroneAboveLimit(PointFeature feature)
    {
        if (!TryGetIntersectingVerticalLimit(feature, out var verticalLimit))
            feature[DroneAboveLimitFeatureField] = null;

        var altitudeMeters = feature.GetScoutData()?.Odid.Location?.AltitudeBaro;
        feature[DroneAboveLimitFeatureField] = altitudeMeters >= verticalLimit ? true : null; // Drone is dangerous when it's high up
    }

    private static bool IsFeatureInactive(PointFeature existingFeature, DateTime utcNow)
    {
        var updateTimeStamp = existingFeature.GetScoutData()?.GetTimestamp();
        if (updateTimeStamp is null) // if the feature doesn't have a timestamp, we will update it to stale later
            return false;

        return utcNow - updateTimeStamp > TimeSpan.FromSeconds(InactiveThresholdSeconds);
    }

    private void RestartTimer()
    {
        _timer.Change(TimeSpan.FromSeconds(StaleThresholdSeconds), Timeout.InfiniteTimeSpan);
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

    private void SetLabelStyle(PointFeature feature)
    {
        var displayText = GetDisplayText(feature);
        var labelStyle = feature.Styles.OfType<LabelStyle>().SingleOrDefault();
        if (labelStyle is null)
        {
            labelStyle = new LabelStyle
            {
                BackColor = _normalLabelColor,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                Offset = new RelativeOffset(0, .9),
                Opacity = 0.3f, // doesn't seem to work
                Font = new Font()
                {
                    Size = 9,
                    FontFamily = "Arial",
                },
            };
            feature.Styles.Add(labelStyle);
        }

        labelStyle.Text = displayText;
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

        var match = _verticalLimitValueRegex.Match(verticalLimitString);
        if (!match.Success)
        {
            LogExtensions.LogError("Failed to extract vertical limit value from string: {0}.", this,
                verticalLimitString);
            return false;
        }

        var success = int.TryParse(match.Groups["metersValue"].Value, out var intValue);
        if (!success)
        {
            LogExtensions.LogError("Failed to extract vertical limit numerical value from regex match: {0}.",
                this, match.Value);
            return false;
        }

        verticalLimit = intValue;
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
        if (disposing)
        {
            _timer.Dispose();
            _semaphore.Dispose();
        }

        base.Dispose(disposing);
    }

    public sealed override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}