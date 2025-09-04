using System;
using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingTrajectoryLayer(IProvider dataSource)
    : UpdatingLayer<Dictionary<string, Queue<PointFeature>>, Queue<PointFeature>>(dataSource)
{
    protected override Dictionary<string, Queue<PointFeature>> Features { get; } = new();

    protected override void UpdateFeaturePositions(IEnumerable<PointFeature> updatedFeatures)
    {
        throw new NotImplementedException();
    }

    protected override IEnumerable<IFeature> ConvertToFeaturesOnInterface(
        Dictionary<string, Queue<PointFeature>> featuresImpl)
    {
        throw new NotImplementedException();
    }

    public override void ClearCache()
    {
        throw new NotImplementedException();
    }
}