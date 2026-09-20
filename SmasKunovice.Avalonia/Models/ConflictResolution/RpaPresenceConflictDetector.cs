using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class RpaPresenceConflictDetector(IntersectionDetector rpaIntersectionDetector) : IConflictDetector
{
    public bool IsInConflictZone(PointFeature feature)
    {
        if (!rpaIntersectionDetector.TryGetIntersectFeature(feature, out _))
            return false;

        var scoutData = feature.GetScoutData();
        var isInZoneVertical = scoutData.Odid.Location?.AltitudeBaro <= 1600.FeetToMeter() || scoutData.Odid.Location?.IsGrounded is true;
        isInZoneVertical = isInZoneVertical || scoutData.IsVehicle();
        if (isInZoneVertical)
            LogExtensions.LogInfo($"Found feature {feature.GetScoutDataId()} in RPA zone.");
        else
            LogExtensions.LogInfo($"Found feature {feature.GetScoutDataId()} in RPA zone horizontal, but not vertical.");
        return isInZoneVertical;
    }
}