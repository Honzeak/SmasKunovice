using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using NetTopologySuite.Index.Strtree;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class IntersectionDetector(STRtree<IFeature> spatialIndex)
{
    public bool TryGetIntersectFeature(PointFeature feature, out IFeature? intersectFeature)
    {
        var candidates = spatialIndex.Query(feature.Extent.ToEnvelope());
        var point = feature.Point.ToPoint();
        intersectFeature = candidates?
            .OfType<GeometryFeature>()
            .FirstOrDefault(candidate => candidate.Geometry is not null && candidate.Geometry.Intersects(point));

        return intersectFeature is not null;
    }
}