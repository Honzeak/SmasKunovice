using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer(IProvider dataSource)
    : UpdatingLayer<PointFeature>(dataSource)
{
    // protected override Dictionary<string, PointFeature> Features { get; } = [];

    protected override void UpdateFeaturePositions(IEnumerable<PointFeature> updateFeatures)
    {
        foreach (var updatedFeature in updateFeatures)
        {
            var id = updatedFeature.GetFeatureId(ScoutData.FeatureUasIdField);
            if (id is null)
                continue;

            Features[id] = updatedFeature;
        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }

    protected override void ApplyFeaturesLabelStyle()
    {
        foreach (var feature in Features)
        {
            var scoutData = feature.Value.GetScoutData();
            string displayText;
            if (scoutData is null)
                displayText = "???";
            else
            {
                displayText = $"Position: {scoutData.Odid?.Location?.Longitude.ToString() ?? "?"}, {scoutData?.Odid?.Location?.Latitude.ToString() ?? "?"} \n" +
                              $"H. Speed: {scoutData?.Odid?.Location?.SpeedHorizontal?.ToString("N") ?? "?"} m/s\n" +
                              $"V. Speed: {scoutData?.Odid?.Location?.SpeedVertical?.ToString("N") ?? "?"} m/s";
            }
            feature.Value.Styles.Add(new LabelStyle
            {
                Text = displayText,
                BackColor = new Brush(Color.Gray),
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                Offset = new RelativeOffset(0,.9),
                Opacity = 0.3f, // doesn't seem to work
                Font = new Font(){
                    Size = 9,
                    FontFamily = "Arial",
                }
            });
        }
    }
}