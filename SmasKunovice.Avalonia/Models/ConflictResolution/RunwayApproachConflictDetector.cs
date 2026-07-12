using System;
using System.Diagnostics.CodeAnalysis;
using Mapsui.Layers;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class RunwayApproachConflictDetector(Coordinate runwayStartPoint, IntersectionDetector approachZoneDetector)
{
    private const double WarningThresholdSeconds = 30;
    private const double AlarmThresholdSeconds = 15;

    public bool IsInConflictZone(PointFeature feature)
    {
        var scoutData = feature.GetScoutData();
        var heading = scoutData.Odid.Location?.Direction;
        var height = scoutData.Odid.Location?.AltitudeBaro;

        if (heading is null || height is null)
        {
            LogExtensions.LogWarning("Missing heading or height data when calculating conflict for feature ID: {0}", this, scoutData.GetUasId());
            return false;
        }

        if (height.Value.MeterToFeet() > 1600 || heading is < 105 or > 295 || !approachZoneDetector.TryGetIntersectFeature(feature, out _))
            return false;

        var timeToTargetSeconds = CalculateTemporalDistanceSeconds(feature);
        if (timeToTargetSeconds >= 0)
            return timeToTargetSeconds <= WarningThresholdSeconds;

        LogExtensions.LogWarning("Missing or invalid horizontal velocity when calculating conflict for feature ID: {0}", this, feature.GetScoutDataId());
        return false;
    }

    private double CalculateTemporalDistanceSeconds(PointFeature feature)
    {
        var velocity = feature.GetScoutData().Odid.Location?.SpeedHorizontal;
        // Prevent division by zero or negative time
        if (velocity is null or <= 0)
            return -1;

        var deltaX = runwayStartPoint.X - feature.Point.X;
        var deltaY = runwayStartPoint.Y - feature.Point.Y;

        // Simple Pythagorean theorem works natively for EPSG:5514
        var distanceMeters = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        var timeToTargetSeconds = distanceMeters / velocity.Value;
        return timeToTargetSeconds;
    }

    public ConflictLevel GetConflictLevel(PointFeature feature)
    {
        var temporalDistanceSeconds = CalculateTemporalDistanceSeconds(feature);
        if (temporalDistanceSeconds < 0)
        {
            LogExtensions.LogWarning("Missing or invalid horizontal velocity when calculating conflict for feature ID: {0}", this, feature.GetScoutDataId());
            return ConflictLevel.None;
        }

        if (temporalDistanceSeconds > WarningThresholdSeconds)
        {
            LogExtensions.LogWarning("Expected to calculate temporal distance ({0} seconds) smaller than warning threshold for feature ID: {1}", this, temporalDistanceSeconds, feature.GetScoutDataId());
            return ConflictLevel.None;
        }
        
        return temporalDistanceSeconds > AlarmThresholdSeconds ? ConflictLevel.Warning : ConflictLevel.Alarm;
    }
}