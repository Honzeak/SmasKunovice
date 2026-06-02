using Mapsui.Layers;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class ConflictFeature(PointFeature feature, ConflictLevel conflictLevel)
{
    public PointFeature Feature { get; } = feature;
    public ConflictLevel ConflictLevel { get; set; } = conflictLevel;
}