using System.IO;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using NetTopologySuite.Index.Strtree;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class IntersectionDetector
{
    private readonly STRtree<IFeature> _spatialIndex;

    public IntersectionDetector(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("GeoJson file not found", path);

        var droneGridFeatureProvider = new GeoJsonFeaturesProvider(path);
        _spatialIndex = new STRtree<IFeature>();
        foreach (var feature in droneGridFeatureProvider.Features) // the params don't really matter for Layer type
        {
            if (feature.Extent is null)
            {
                LogExtensions.LogWarning("Intersection detection feature has null extent. Skipping.", this);
                continue;
            }

            _spatialIndex.Insert(feature.Extent.ToEnvelope(), feature);
        }

        _spatialIndex.Build();
    }

    public bool TryGetIntersectFeature(PointFeature feature, out IFeature? intersectFeature)
    {
        var candidates = _spatialIndex.Query(feature.Extent.ToEnvelope());
        var point = feature.Point.ToPoint();
        intersectFeature = candidates?
            .OfType<GeometryFeature>()
            .FirstOrDefault(candidate => candidate.Geometry is not null && candidate.Geometry.Intersects(point));

        return intersectFeature is not null;
    }
}