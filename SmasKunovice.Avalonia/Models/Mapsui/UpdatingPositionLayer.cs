using System.Collections.Generic;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
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

            var existingFeature = FindExistingFeature(id);
            if (existingFeature is null)
            {
                // Create new feature if it doesn't exist
                var newFeature = new PointFeature(updatedFeature.Point.X, updatedFeature.Point.Y);
                CopyFeatureFields(updatedFeature, newFeature);
                Features.Add(id, newFeature);
            }
            else
            {
                // Update existing feature position immediately
                existingFeature.Point.X = updatedFeature.Point.X;
                existingFeature.Point.Y = updatedFeature.Point.Y;
                CopyFeatureFields(updatedFeature, existingFeature);
            }
        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }
}