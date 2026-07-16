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
    private static readonly string ApproachZoneAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "ApproachProximityZones.geojson"));
    private static readonly string RunwayStartPointAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "runwayStartPoints.geojson"));
    private static readonly string DroneGridAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "DroneGridCtr.geojson"));

    public static (RunwayApproachConflictDetector _02C, RunwayApproachConflictDetector _20C) CreateForRunwayApproach() =>
    (
        CreateForRunwayApproach(RunwayDirection._02C),
        CreateForRunwayApproach(RunwayDirection._20C)
    );

    private static RunwayApproachConflictDetector CreateForRunwayApproach(RunwayDirection direction)
    {
        if (!File.Exists(RunwayStartPointAssetPath))
            throw new FileNotFoundException("Asset file not found", RunwayStartPointAssetPath);
        if (!File.Exists(ApproachZoneAssetPath))
            throw new FileNotFoundException("Asset file not found", ApproachZoneAssetPath);

        var geometryFeature = new GeoJsonFeaturesProvider(RunwayStartPointAssetPath).Features
            .Where(gf => MatchFeatureForRunwayDirection(gf, direction))
            .Cast<GeometryFeature>().SingleOrDefault();
        
        LogExtensions.LogDebug($"Adding feature with extent [{geometryFeature?.Extent}] to intersection detector for asset [{Path.GetFileName(RunwayStartPointAssetPath)}]");
        
        return geometryFeature?.Geometry is null 
            ? throw new InvalidOperationException($"Failed to find runway start point geometry for direction {direction}") 
            : new RunwayApproachConflictDetector(geometryFeature.Geometry.Coordinate, CreateIntersectionDetector(ApproachZoneAssetPath, direction), direction);
    }

    public static DroneAboveLimitConflictDetector CreateForDroneGrid()
    {
        return File.Exists(DroneGridAssetPath) ? new DroneAboveLimitConflictDetector(CreateIntersectionDetector(DroneGridAssetPath)) : throw new FileNotFoundException("Asset file not found", DroneGridAssetPath);
    }

    public static RpaPresenceConflictDetector CreateForRpaPresence()
    {
        return File.Exists(RpaAssetPath) ? new RpaPresenceConflictDetector(CreateIntersectionDetector(RpaAssetPath)) : throw new FileNotFoundException("Asset file not found", RpaAssetPath);
    }

    public static IntersectionDetector CreateIntersectionDetector(string assetPath, RunwayDirection? direction = null)
    {
        var spatialIndex = new STRtree<IFeature>();
        var featureProvider = new GeoJsonFeaturesProvider(assetPath);
        var counter = 0;
        foreach (var feature in featureProvider.Features) // the params don't really matter for Layer type
        {
            if (feature.Extent is null)
            {
                LogExtensions.LogWarning("Intersection detection feature has null extent. Skipping.");
                continue;
            }

            if (direction.HasValue && !MatchFeatureForRunwayDirection(feature, direction.Value))
                continue;

            if (direction is not null)
                LogExtensions.LogDebug($"Adding feature with extent [{feature.Extent}] to intersection detector for asset [{Path.GetFileName(assetPath)}]");
            
            spatialIndex.Insert(feature.Extent.ToEnvelope(), feature);
            counter++;
        }

        spatialIndex.Build();
        LogExtensions.LogDebug($"Created intersection detector for asset [{Path.GetFileName(assetPath)}] with [{counter}] features");
        return new IntersectionDetector(spatialIndex);
    }
    
    private static bool MatchFeatureForRunwayDirection(IFeature gf, RunwayDirection direction)
    {
        var zoneDirectionString = gf["zone"]?.ToString();
        ArgumentException.ThrowIfNullOrEmpty(zoneDirectionString);
        var zoneDirection = Enum.Parse<RunwayDirection>(zoneDirectionString);
        return zoneDirection == direction;
    }
}