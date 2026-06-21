using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class RpaPresenceConflictDetector
{
    public bool IsRpaPresence => _featuresInRpa.Count > 0;
    private readonly List<PointFeature> _featuresInRpa = [];
    private readonly IntersectionDetector _rpaIntersectionDetector;
    private bool _isConflictExternal;
    private ConflictLevel _externalConflictLevel;

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

    private ConflictLevel GetConflictLevel()
    {
        if (_featuresInRpa.Count(f => f.GetScoutData()?.IsVehicle() == false) >= 2)
            return ConflictLevel.Alarm;

        return _isConflictExternal ? _externalConflictLevel : ConflictLevel.None;
    }

    public IEnumerable<ConflictFeature> GetConflictFeatures()
    {
        return _featuresInRpa.Select(f => new ConflictFeature(f, GetConflictLevel()));
    }

    public void Reset()
    {
        _isConflictExternal = false;
        _externalConflictLevel = ConflictLevel.None;
        _featuresInRpa.Clear();
    }

    public void SetConflict(ConflictLevel conflictLevel)
    {
        _isConflictExternal = true;
        _externalConflictLevel = conflictLevel;
    }
}