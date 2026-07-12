using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Mapsui.Layers;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class ConflictsUpdateEventArgs
{
    [MemberNotNullWhen(true, nameof(Added))]
    [MemberNotNullWhen(true, nameof(Modified))]
    [MemberNotNullWhen(true, nameof(Removed))]
    [MemberNotNullWhen(false, nameof(UpdateResult))]
    [MemberNotNullWhen(false, nameof(Feature))]
    public bool IsEnumerable => Feature is null;
    public IReadOnlySet<PointFeature>? Added { get; }
    public IReadOnlySet<PointFeature>? Modified { get; }
    public IReadOnlySet<PointFeature>? Removed { get; }
    public ConflictUpdateResult? UpdateResult { get; }
    public PointFeature? Feature { get;  }
    public ConflictType ConflictType { get; }
    public ConflictLevel ConflictLevel { get; }
    
    private ConflictsUpdateEventArgs(ConflictType conflictType, ConflictLevel conflictLevel)
    {
        ConflictType = conflictType;
        ConflictLevel = conflictLevel;
    }

    public ConflictsUpdateEventArgs(PointFeature pointFeature, ConflictUpdateResult updateResult, ConflictType conflictType, ConflictLevel conflictLevel) : this(conflictType, conflictLevel)
    {
        Feature = pointFeature;
        UpdateResult = updateResult;
    }

    public ConflictsUpdateEventArgs(ConflictUpdateResults updateResults, Dictionary<string, PointFeature> pointFeatures, ConflictType conflictType, ConflictLevel conflictLevel) : this(conflictType, conflictLevel)
    {
        Added = updateResults.Added.Select(x => pointFeatures[x]).ToHashSet();
        Modified = updateResults.Modified.Select(x => pointFeatures[x]).ToHashSet();
        Removed = updateResults.Removed.Select(x => pointFeatures[x]).ToHashSet();
    }
}