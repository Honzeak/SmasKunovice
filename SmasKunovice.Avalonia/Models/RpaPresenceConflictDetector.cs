using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class RpaPresenceConflictDetector
{
    private bool IsConflict =>
        _featuresInRpa.Any(f =>
        {
            var scoutData = f.GetScoutData();
            return scoutData is not null && !scoutData.IsVehicle();
        }) && _featuresInRpa.Count > 1;

    public bool IsRpaPresence => _featuresInRpa.Count > 0;
    private readonly List<PointFeature> _featuresInRpa = [];
    private readonly IntersectionDetector _rpaIntersectionDetector;

    public RpaPresenceConflictDetector(string rpaAssetPath)
    {
        if (!File.Exists(rpaAssetPath))
            throw new FileNotFoundException("Rpa asset file not found", rpaAssetPath);

        _rpaIntersectionDetector = new IntersectionDetector(rpaAssetPath);
    }

    public bool ProcessConflictCandidate(PointFeature feature)
    {
        if (!_rpaIntersectionDetector.TryGetIntersectFeature(feature, out _))
            return false;

        if (feature.GetScoutData()?.Odid.Location?.AltitudeBaro > 0)
            return false;
            
        _featuresInRpa.Add(feature);
        return true;
    }

    public IEnumerable<ConflictFeature> GetConflictFeatures()
    {
        return _featuresInRpa.Select(f => new ConflictFeature(f, IsConflict ? ConflictLevel.Alarm : ConflictLevel.None));
    }

    public void Reset()
    {
        _featuresInRpa.Clear();
    }
}