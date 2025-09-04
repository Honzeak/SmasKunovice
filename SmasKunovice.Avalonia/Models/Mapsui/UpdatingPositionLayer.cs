using System.Collections.Generic;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingPositionLayer(IProvider dataSource)
    : UpdatingLayer<Dictionary<string, PointFeature>, PointFeature>(dataSource)
{
    protected override Dictionary<string, PointFeature> Features { get; } = [];

    protected override void UpdateFeaturePositions(IEnumerable<PointFeature> updatedFeatures)
    {
        foreach (var updatedFeature in updatedFeatures)
        {
            var id = updatedFeature.GetFeatureId();
            if (id is null)
                continue;

            var existingFeature = FindExistingFeature(Features, id);
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

    protected override IEnumerable<IFeature> ConvertToFeaturesOnInterface(Dictionary<string, PointFeature> featuresImpl)
    {
        return Features.Select(f => f.Value);
    }

    public override void ClearCache()
    {
        Features.Clear();
    }
}