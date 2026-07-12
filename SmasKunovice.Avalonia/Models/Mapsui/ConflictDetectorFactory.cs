using System;
using System.IO;
using System.Linq;
using Mapsui;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using NetTopologySuite.Index.Strtree;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.ConflictResolution;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public static class ConflictDetectorFactory
{
    private static readonly string RpaAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "Rpa.geojson"));
    private static readonly string ApproachZoneAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "ApproachProximityZone.geojson"));
    private static readonly string RunwayStartPointAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "runwayStartPoint.geojson"));
    private static readonly string DroneGridAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "DroneGridCtr.geojson"));

    public static RunwayApproachConflictDetector CreateForRunwayApproach()
    {
        if (!File.Exists(RunwayStartPointAssetPath))
            throw new FileNotFoundException("Asset file not found", RunwayStartPointAssetPath);
        if (!File.Exists(ApproachZoneAssetPath))
            throw new FileNotFoundException("Asset file not found", ApproachZoneAssetPath);

        var geometryFeature = new GeoJsonFeaturesProvider(RunwayStartPointAssetPath).Features.Cast<GeometryFeature>().SingleOrDefault();
        return geometryFeature?.Geometry is null ?
            throw new InvalidOperationException("Failed to find runway start point geometry") :
            new RunwayApproachConflictDetector(geometryFeature.Geometry.Coordinate, CreateIntersectionDetector(ApproachZoneAssetPath));
    }

    public static DroneAboveLimitConflictDetector CreateForDroneGrid()
    {
        return File.Exists(DroneGridAssetPath) ?
            new DroneAboveLimitConflictDetector(CreateIntersectionDetector(DroneGridAssetPath)) :
            throw new FileNotFoundException("Asset file not found", DroneGridAssetPath);
    }

    public static RpaPresenceConflictDetector CreateForRpaPresence()
    {
        return File.Exists(RpaAssetPath) ?
            new RpaPresenceConflictDetector(CreateIntersectionDetector(RpaAssetPath)) :
            throw new FileNotFoundException("Asset file not found", RpaAssetPath);
    }

    public static IntersectionDetector CreateIntersectionDetector(string assetPath)
    {
        var spatialIndex = new STRtree<IFeature>();
        var featureProvider = new GeoJsonFeaturesProvider(assetPath);
        foreach (var feature in featureProvider.Features) // the params don't really matter for Layer type
        {
            if (feature.Extent is null)
            {
                LogExtensions.LogWarning("Intersection detection feature has null extent. Skipping.");
                continue;
            }

            spatialIndex.Insert(feature.Extent.ToEnvelope(), feature);
        }

        spatialIndex.Build();
        return new IntersectionDetector(spatialIndex);
    }
}