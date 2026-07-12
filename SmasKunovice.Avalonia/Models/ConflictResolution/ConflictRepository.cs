using System;
using System.Collections.Generic;
using System.Threading;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public record ConflictUpdateResults(
    IReadOnlySet<string> Added,
    IReadOnlySet<string> Modified,
    IReadOnlySet<string> Unchanged,
    IReadOnlySet<string> Removed
) 
{
    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Removed.Count > 0;
}

public enum ConflictUpdateResult
{
    Added,
    Modified,
    Unchanged,
    Removed
}

public enum ConflictType
{
    DroneAboveLimit,
    RpaPresence,
    RunwayApproach
}

public class ConflictRepository
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Dictionary<ConflictType, ConflictLevel>> _storage = new();

    public ConflictUpdateResult UpdateConflict(string uasId, ConflictType conflictType, ConflictLevel conflictLevel)
    {
        lock (_lock)
        {
            if (_storage.TryGetValue(uasId, out var featureConflicts))
            {
                if (featureConflicts.TryGetValue(conflictType, out var existingConflictLevel))
                {
                    if (conflictLevel is ConflictLevel.None)
                    {
                        var removeResult = featureConflicts.Remove(conflictType, out _);
                        return removeResult ? ConflictUpdateResult.Removed : ConflictUpdateResult.Unchanged; // This has to option 1 always.. 
                    }

                    if (existingConflictLevel == conflictLevel)
                        return ConflictUpdateResult.Unchanged;
                    featureConflicts[conflictType] = conflictLevel;
                    return ConflictUpdateResult.Modified;
                }

                if (conflictLevel is ConflictLevel.None)
                    return ConflictUpdateResult.Unchanged;

                featureConflicts.Add(conflictType, conflictLevel);
                return ConflictUpdateResult.Added;
            }

            if (conflictLevel is ConflictLevel.None)
                return ConflictUpdateResult.Unchanged;

            _storage.Add(uasId, new Dictionary<ConflictType, ConflictLevel> { { conflictType, conflictLevel } });
            return ConflictUpdateResult.Added;
        }
    }

    public ConflictUpdateResults UpdateConflicts(IEnumerable<string> conflictZoneFeatures, ConflictType conflictType, ConflictLevel conflictLevel)
    {
        lock (_lock)
        {
            var added = new HashSet<string>();
            var modified = new HashSet<string>();
            var unchanged = new HashSet<string>();
            var removed = new HashSet<string>();
            foreach (var uasId in conflictZoneFeatures)
            {
                switch (UpdateConflict(uasId, conflictType, conflictLevel))
                {
                    case ConflictUpdateResult.Added:
                        added.Add(uasId);
                        break;
                    case ConflictUpdateResult.Modified:
                        modified.Add(uasId);
                        break;
                    case ConflictUpdateResult.Unchanged:
                        unchanged.Add(uasId);
                        break;
                    case ConflictUpdateResult.Removed:
                        removed.Add(uasId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return new ConflictUpdateResults(added, modified, unchanged, removed);
        }
    }

    public void RemoveConflict(string uasId, ConflictType conflictType)
    {
        lock (_lock)
        {
            if (!_storage.TryGetValue(uasId, out var conflictTypes))
                return;

            conflictTypes.Remove(conflictType);
        }
    }

    public bool RemoveById(string featureId)
    {
        lock (_lock)
        {
            return _storage.Remove(featureId, out _);
        }
    }
}