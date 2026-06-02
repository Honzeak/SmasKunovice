using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Layers;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class RunwayApproachConflictDetector
{
    private const double WarningThresholdSeconds = 30;
    private const double AlarmThresholdSeconds = 15;
    private readonly List<ConflictFeature> _conflictFeatures = [];
    private readonly Coordinate _runwayStartPoint;
    private readonly IntersectionDetector _approachZoneDetector;

    public RunwayApproachConflictDetector(string runwayStartPointAssetPath, string approachZoneAssetPath)
    {
        if (!File.Exists(runwayStartPointAssetPath))
            throw new FileNotFoundException("Asset file not found", runwayStartPointAssetPath);
        if (!File.Exists(approachZoneAssetPath))
            throw new FileNotFoundException("Asset file not found", approachZoneAssetPath);
        
        var geometryFeature = new GeoJsonFeaturesProvider(runwayStartPointAssetPath).Features.Cast<GeometryFeature>().Single();
        if (geometryFeature.Geometry is null)
            throw new InvalidOperationException("Failed to find runway start point geometry");
            
        _runwayStartPoint = geometryFeature.Geometry.Coordinate;
        _approachZoneDetector = new IntersectionDetector(approachZoneAssetPath);
    }

    public bool ProcessConflictCandidate(PointFeature feature)
    {
        var scoutData = feature.GetScoutData();
        var heading = scoutData?.Odid.Location?.Direction;
        var height = scoutData?.Odid.Location?.AltitudeBaro;

        if (heading is null || height is null)
        {
            LogExtensions.LogWarning("Missing heading or height data when calculating conflict for feature ID: {0}", this, scoutData?.GetUasId() ?? "UNKNOWN");
            return false;
        }

        if (height.Value.MeterToFeet() > 1600)
            return false;

        if (heading is < 105 or > 295)
            return false;
        
        if (!_approachZoneDetector.TryGetIntersectFeature(feature, out _))
            return false;

        var velocity = scoutData?.Odid.Location?.SpeedHorizontal;
        // Prevent division by zero or negative time
        if (velocity is null or <= 0)
        {
            LogExtensions.LogWarning("Missing or invalid horizontal velocity ({0}) when calculating conflict for feature ID: {0}", this, velocity.ToString() ?? "N/A", scoutData?.GetUasId() ?? "UNKNOWN");
            return false;
        }

        var deltaX = _runwayStartPoint.X - feature.Point.X;
        var deltaY = _runwayStartPoint.Y - feature.Point.Y;

        // Simple Pythagorean theorem works natively for EPSG:5514
        var distanceMeters = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        var timeToTargetSeconds = distanceMeters / velocity.Value;
        if (timeToTargetSeconds > WarningThresholdSeconds)
            return false;
        
        LogExtensions.LogInfo("Adding approach conflict candidate with ID: {0}", this, feature.GetScoutDataId());
        _conflictFeatures.Add(new ConflictFeature(feature, timeToTargetSeconds > AlarmThresholdSeconds ? ConflictLevel.Warning : ConflictLevel.Alarm));
        return true;
    }

    public IEnumerable<ConflictFeature> GetConflictFeatures(RpaPresenceConflictDetector detector)
    {
        if (!detector.IsRpaPresence)
        {
            foreach (var conflictFeature in _conflictFeatures)
                conflictFeature.ConflictLevel = ConflictLevel.None;
        }
        
        return _conflictFeatures;
    }
    
    public void Reset()
    {
        _conflictFeatures.Clear();
    }
    
}