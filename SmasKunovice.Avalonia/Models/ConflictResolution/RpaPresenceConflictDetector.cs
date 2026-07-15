using System.IO;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class RpaPresenceConflictDetector(IntersectionDetector rpaIntersectionDetector)
{
    public bool IsInConflictZone(PointFeature feature)
    {
        if (!rpaIntersectionDetector.TryGetIntersectFeature(feature, out _))
            return false;

        return feature.GetScoutData().Odid.Location?.IsGrounded ?? false;
    }
}