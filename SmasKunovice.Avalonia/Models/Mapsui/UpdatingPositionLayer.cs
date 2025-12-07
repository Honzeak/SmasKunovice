using System.Collections.Generic;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer : UpdatingLayer<PointFeature>
{
    private readonly IAircraftDatabase _aircraftDatabase;
    private readonly IAircraftSymbolProvider _aircraftSymbolProvider;
    private const string SelectedFeatureField = "selected";
    private IFeature? _currentSelectedFeature;

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
            if (f[SelectedFeatureField]?.ToString() == "true")

                return new StyleCollection
                {
                    Styles =
                    {
                        CreateSymbol(f, true),
                        CreateSymbol(f, false)
                    }
                };

            return CreateSymbol(f, false);
        });
    }

    private IStyle CreateSymbol(IFeature feature, bool selectedStyle)
    {
        var scoutData = feature.GetScoutData();
        return scoutData?.Tech switch
        {
            "B4" or "B5" or "WN" or "WB" => _aircraftSymbolProvider.GetDroneStyle(selectedStyle),
            _ => _aircraftSymbolProvider.GetAirplaneStyle(selectedStyle) // default to airplane
        };
    }

    protected override void ProcessFeatures(IEnumerable<PointFeature> updateFeatures)
    {
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
                existingFeature.Point.X = updatedFeature.Point.X;
                existingFeature.Point.Y = updatedFeature.Point.Y;
                existingFeature[ScoutData.FeatureScoutDataField] = updatedFeature.GetScoutData();
                SetLabelStyle(existingFeature);
            }
            else
            {
                Features[id] = updatedFeature;
                SetLabelStyle(updatedFeature);
            }

        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }

    private void SetLabelStyle(IFeature feature)
    {
        var scoutData = feature.GetScoutData();
        var displayText = scoutData is null ? "???" : GetDisplayText(scoutData);
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

    private string GetDisplayText(ScoutData scoutData)
    {
        var uasId = scoutData.GetUasId();
        var aircraftRecord = _aircraftDatabase.GetByIcao24(uasId);
        var registration = aircraftRecord?.Registration ?? uasId;

        var heightString = GetHeightString(scoutData);
        var speedString = GetSpeedString(scoutData);

        var displayText = $"{registration} \n" +
                          $"{heightString} " +
                          $"{speedString}";

        return displayText;
    }

    private static string GetSpeedString(ScoutData scoutData)
    {
        const double mpsToKnotFactor = 1.94384;
        var speedKnots = scoutData.Odid?.Location?.SpeedHorizontal;
        return speedKnots is null ? "?" : $"{speedKnots * mpsToKnotFactor:F0}";
    }

    private static string GetHeightString(ScoutData scoutData)
    {
        const double meterToFeetConvertFactor = 3.28084;
        var verticalSpeed = scoutData.Odid?.Location?.SpeedVertical;
        var verticalSpeedSymbol = verticalSpeed switch
        {
            > 0 and <= 62 => "↑",
            < 0 and >= -62 => "↓",
            _ => string.Empty
            // null or 0 or <= -63 or >= 63 => string.Empty,
        };
        var scoutDataAltitude = scoutData.Odid?.Location?.AltitudeGeo;
        string heightValue;
        if (scoutDataAltitude is null)
        {
            heightValue = "?";
        }
        else
        {
            var feet = (int)(scoutDataAltitude.Value * meterToFeetConvertFactor);
            heightValue = feet <= 5000 ? feet.ToString() : $"FL{feet / 100f:F0}";
        }

        return heightValue + verticalSpeedSymbol;
    }

    public void SetLabelVisibility(bool visible)
    {
        if (_currentSelectedFeature is null) return;
        var style = _currentSelectedFeature.Styles.OfType<LabelStyle>().FirstOrDefault();
        if (style != null) style.Enabled = visible;
    }
}