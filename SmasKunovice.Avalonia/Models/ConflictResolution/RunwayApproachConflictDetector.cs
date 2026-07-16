using System;
using Mapsui.Layers;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class RunwayApproachConflictDetector
{
    private const int WarningThresholdSeconds = 30;
    private const int AlarmThresholdSeconds = 15;
    private const int HeadingThresholdOffset = 95; 
    private readonly int _runwayDirectionDegrees;
    private readonly HeadingRangeEvaluator _headingRangeEvaluator;

    private readonly Coordinate _runwayStartPoint;
    private readonly IntersectionDetector _approachZoneDetector;

    public RunwayApproachConflictDetector(Coordinate runwayStartPoint, IntersectionDetector approachZoneDetector, RunwayDirection runwayDirection)
    {
        _runwayStartPoint = runwayStartPoint;
        _approachZoneDetector = approachZoneDetector;
        _runwayDirectionDegrees = runwayDirection switch
        {
            RunwayDirection._02C => 20,
            RunwayDirection._20C => 200,
            _ => throw new ArgumentOutOfRangeException(nameof(runwayDirection), runwayDirection.ToString(), null)
        };
        
        _headingRangeEvaluator = new HeadingRangeEvaluator(_runwayDirectionDegrees, HeadingThresholdOffset);
    }


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

        if (height.Value.MeterToFeet() > 1600 
            || !_headingRangeEvaluator.IsWithinBounds(heading.Value)
            || !_approachZoneDetector.TryGetIntersectFeature(feature, out _))
            return false;

        var timeToTargetSeconds = CalculateTemporalDistanceSeconds(feature);
        LogExtensions.LogDebug($"Found feature in approach zone [{_runwayDirectionDegrees}] with time to target [{timeToTargetSeconds}] s");
        
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

        var deltaX = _runwayStartPoint.X - feature.Point.X;
        var deltaY = _runwayStartPoint.Y - feature.Point.Y;

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