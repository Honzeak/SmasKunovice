using System.Collections.Generic;
using System;
using System.Globalization;
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
    private readonly IAircraftSymbolProvider _aircraftSymbolProvider;
    private const string SelectedFeatureField = "selected";
    private const string StaleFeatureField = "stale";
    private IFeature? _currentSelectedFeature;
    private const int StaleThresholdSeconds = 5;
    private const int InactiveThresholdSeconds = 60;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event EventHandler<IFeature?>? SelectedFeatureChanged;

    public UpdatingPositionLayer(IProvider dataSource,
        IAircraftDatabase aircraftDatabase,
        IAircraftSymbolProvider aircraftSymbolProvider,
        Map map) : base(dataSource)
    {
        _aircraftDatabase = aircraftDatabase;
        _aircraftSymbolProvider = aircraftSymbolProvider;
        map.Info += (sender, e) => ToggleSelected(e.MapInfo?.Feature);
        IsMapInfoLayer = true;
        Style = CreateStyle();
        _timer = new Timer(_ => UpdateDataAsync(false).GetAwaiter().GetResult(), null, TimeSpan.FromSeconds(StaleThresholdSeconds), Timeout.InfiniteTimeSpan);
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

    private IStyle CreateStyle()
    {
        return new ThemeStyle(f =>
        {
            var state = SymbolState.Normal;
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
            _ => _aircraftSymbolProvider.GetAirplaneStyle(selectedStyle) // default to airplane
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
                var id = updatedFeature.GetFeatureId(ScoutData.FeatureUasIdField);
                if (id is null)
                {
                    LogExtensions.LogError("Failed to get feature ID. Cannot update position.", this);
                    continue;
                }

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
                    SetLabelStyle(existingFeature);
                    SetStaleFeature(existingFeature, utcNow); // Grey out stale features
                }
                else
                {
                    Features[id] = updatedFeature;
                    SetLabelStyle(updatedFeature);
                }
            }

            if (reprocessing) // non-reprocessing call shouldn't restart the timer, since we need to touch all features at least every N seconds
                RestartTimer();
        }
        finally
        {
            _semaphore.Release();
        }
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

    private void SetLabelStyle(IFeature feature)
    {
        var displayText = GetDisplayText(feature);
        var labelStyle = feature.Styles.OfType<LabelStyle>().SingleOrDefault();
        if (labelStyle is null)
        {
            labelStyle = new LabelStyle
            {
                BackColor = new Brush(Color.WhiteSmoke),
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

    private string GetDisplayText(IFeature feature)
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