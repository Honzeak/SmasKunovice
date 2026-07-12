using System;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class DroneAboveLimitConflictDetector(IntersectionDetector droneGridIntersectionDetector)
{
    public bool IsInConflictZone(PointFeature feature)
    {
        if (feature.GetScoutData().IsDrone() is not true)
            return false;

        if (!TryGetIntersectingVerticalLimit(feature, out var verticalLimit))
            return false;

        var altitudeMeters = feature.GetScoutData().Odid.Location?.AltitudeBaro;
        return altitudeMeters >= verticalLimit; // Drone is dangerous when it's high up
    }

    private bool TryGetIntersectingVerticalLimit(PointFeature feature, out int? verticalLimit)
    {
        verticalLimit = null;
        if (droneGridIntersectionDetector.TryGetIntersectFeature(feature, out var candidate) is not true)
            return false;

        var verticalLimitString = candidate!["vertical_limit"]?.ToString();
        if (verticalLimitString is null)
        {
            LogExtensions.LogError("Vertical limit not found on grid feature. Feature ID: {0}.", this,
                feature.GetScoutDataId());
            return false;
        }

        // GND - <num> m AGL
        var dashIndex = verticalLimitString.IndexOf('-');
        var mIndex = verticalLimitString.IndexOf('m');

        if (dashIndex == -1 || mIndex == -1 || mIndex <= dashIndex)
        {
            LogExtensions.LogError("Vertical limit string is not in expected format. Feature ID: {0}.", this,
                feature.GetScoutDataId());
            return false;
        }

        var valueSpan = verticalLimitString.AsSpan(dashIndex + 1, mIndex - dashIndex - 1).Trim();
        var parseResult = int.TryParse(valueSpan, out var verticalLimitValue);

        if (!parseResult)
        {
            LogExtensions.LogError("Failed to extract vertical limit value from string: {0}.", this, valueSpan.ToString());
            return false;
        }

        verticalLimit = verticalLimitValue;
        return true;
    }
}