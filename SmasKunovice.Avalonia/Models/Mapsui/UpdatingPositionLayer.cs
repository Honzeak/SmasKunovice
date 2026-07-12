using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;
using SmasKunovice.Avalonia.Models.Dronetag;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer : UpdatingLayer<PointFeature>
{
    private bool _disposed;
    private readonly IAircraftDatabase _aircraftDatabase;
    private readonly TargetStyleBuilder _targetStyleBuilder;
    private const string SelectedFeatureField = "selected";
    private const string StaleFeatureField = "stale";
    private IFeature? _currentSelectedFeature;
    private const int StaleThresholdSeconds = 5;
    private const int InactiveThresholdSeconds = 60;
    private readonly Brush _normalLabelColor = new(Color.WhiteSmoke);
    private readonly Brush _warningLabelColor = new(new Color(255, 222, 40));
    private readonly Brush _alarmLabelColor = new(new Color(255, 0, 0));
    private readonly Map _map;
    private readonly IConflictDetectionService _conflictDetectionService;

    public event EventHandler<IFeature?>? SelectedFeatureChanged;

    public UpdatingPositionLayer(IProvider dataSource,
        IAircraftDatabase aircraftDatabase,
        TargetStyleBuilder targetStyleBuilder,
        Map map, IErrorDialogService errorDialogService, IConflictDetectionService conflictDetectionService) : base(dataSource, errorDialogService)
    {
        _aircraftDatabase = aircraftDatabase;
        _targetStyleBuilder = targetStyleBuilder;
        _map = map;
        _map.Info += OnMapOnInfo;
        // TODO subscribe to conflict events
        _conflictDetectionService = conflictDetectionService;
        IsMapInfoLayer = true;
        Style = CreateStyle();
    }

    private void OnMapOnInfo(object? sender, MapInfoEventArgs e) => ToggleSelected(e.MapInfo?.Feature);

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
            var scoutData = f.GetScoutData();
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

    protected override Task ProcessFeaturesAsync(IEnumerable<PointFeature> updateFeatures, bool reprocessing)
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            foreach (var updatedFeature in updateFeatures)
            {
                ProcessFeatureUpdates(updatedFeature, utcNow);
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private void ProcessFeatureUpdates(PointFeature updatedFeature, DateTime utcNow)
    {
        var uasId = updatedFeature.GetScoutDataId();
        if (Features.TryGetValue(uasId, out var existingFeature))
        {
            if (IsFeatureInactive(existingFeature, utcNow))
            {
                RemoveFeature(uasId);
                return;
            }

            existingFeature.Point.X = updatedFeature.Point.X;
            existingFeature.Point.Y = updatedFeature.Point.Y;
            existingFeature[FeatureAttributes.ScoutData] = updatedFeature.GetScoutData();
            SetStaleFeature(existingFeature, utcNow); // Grey out stale features
            UpdateLabel(existingFeature);
        }
        else
        {
            Features[uasId] = updatedFeature;
            var aircraftRecord = _aircraftDatabase.GetByIcao24(uasId);
            updatedFeature[FeatureAttributes.AircraftRegistration] = aircraftRecord?.Registration;
            UpdateLabel(updatedFeature);
        }
    }

    protected override bool RemoveFeature(string featureId)
    {
        _conflictDetectionService.RemoveFeature(featureId);
        return base.RemoveFeature(featureId);
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
        var updateTimeStamp = existingFeature.GetScoutData().Odid.Location?.GetTimestamp();
        if (updateTimeStamp is null) // if the feature doesn't have a timestamp, we will update it to stale later
            return false;

        return utcNow - updateTimeStamp > TimeSpan.FromSeconds(InactiveThresholdSeconds);
    }

    private static void SetStaleFeature(PointFeature feature, DateTime utcNow)
    {
        var updateTimeStamp = feature.GetScoutData().Odid.Location?.GetTimestamp();
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

    private void UpdateLabel(PointFeature feature)
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

    private static string GetDisplayText(PointFeature feature)
    {
        var scoutData = feature.GetScoutData();

        var heightString = AircraftDataFormatter.GetHeightString(scoutData);
        var speedString = AircraftDataFormatter.GetSpeedString(scoutData);

        var displayText = $"{feature.GetAircraftDisplayId()} \n" +
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