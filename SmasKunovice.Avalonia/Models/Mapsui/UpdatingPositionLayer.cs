using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer(
    IProvider dataSource,
    IAircraftDatabase aircraftDatabase,
    IAircraftSymbolProvider aircraftSymbolProvider)
    : UpdatingLayer<PointFeature>(dataSource)
{
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
            
            var scoutData = updatedFeature.GetScoutData();
            ApplyFeatureLabelStyle(scoutData, updatedFeature);
            ApplyFeatureSymbolStyle(scoutData, updatedFeature);
            Features[id] = updatedFeature;
        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }

    private void ApplyFeatureSymbolStyle(ScoutData? scoutData, IFeature feature)
    {
        var style = scoutData?.Tech switch
        {
            "B4" or "B5" or "WN" or "WB" => aircraftSymbolProvider.GetDroneStyle(),
            _ => aircraftSymbolProvider.GetAirplaneStyle() // default to airplane
        };
        
        feature.Styles.Add(style);
    }

    private void ApplyFeatureLabelStyle(ScoutData? scoutData, IFeature feature)
    {
        var displayText = scoutData is null ? "???" : GetDisplayText(scoutData);
        feature.Styles.Add(new LabelStyle
        {
            Text = displayText,
            BackColor = new Brush(Color.WhiteSmoke),
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            Offset = new RelativeOffset(0, .9),
            Opacity = 0.3f, // doesn't seem to work
            Font = new Font()
            {
                Size = 9,
                FontFamily = "Arial",
            }
        });
    }

    private string GetDisplayText(ScoutData scoutData)
    {
        var uasId = scoutData.GetUasId();
        var aircraftRecord = aircraftDatabase.GetByIcao24(uasId);
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
}