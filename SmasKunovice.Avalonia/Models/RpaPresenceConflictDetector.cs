using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class RpaPresenceConflictDetector
{
    private bool IsConflict => _featuresInRpa.Count > 1;
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