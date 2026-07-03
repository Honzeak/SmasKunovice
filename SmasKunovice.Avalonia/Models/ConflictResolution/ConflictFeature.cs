using System;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.ConflictResolution;

public class ConflictFeature(PointFeature feature, ConflictLevel conflictLevel, string description) : IEquatable<ConflictFeature>
{
    public PointFeature Feature { get; } = feature;
    public ConflictLevel ConflictLevel { get; private set; } = conflictLevel;
    public string Description { get; } = description;
    public bool IsMuted { get; private set; } = false;

    public bool Equals(ConflictFeature? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Feature.GetScoutDataId() == other.Feature.GetScoutDataId() && Description == other.Description;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConflictFeature other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Feature.GetScoutDataId(), Description);
    }

    public void ResetConflictLevel()
    {
        ConflictLevel = ConflictLevel.None;
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }
}
