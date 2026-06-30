using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SmasKunovice.Avalonia.Models;

public class ConflictRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ConflictType, ConflictFeature>> _storage = new();

    public bool RemoveById(string featureId)
    {
        throw new System.NotImplementedException();
    }

    public void AddConflict(string id, ConflictType type, ConflictFeature feature)
    {
        throw new System.NotImplementedException();
    }
}

public enum ConflictType
{
    DroneAboveLimit,
    RpaPresence,
    RunwayApproach
}
