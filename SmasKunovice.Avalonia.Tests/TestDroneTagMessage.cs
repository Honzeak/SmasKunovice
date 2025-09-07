using Mapsui.Layers;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests;

[Obsolete("Used only for testing, might not need it.")]
public record TestDroneTagMessage(
    string Id,
    double Latitude,
    double Longitude,
    double Altitude,
    double Speed,
    double Heading) : IScoutData
{
    public bool TryCreatePointFeature(out PointFeature? pointFeature)
    {
        pointFeature = new PointFeature(Latitude, Longitude);
        pointFeature[ScoutData.FeatureUasIdField] = Id;
        pointFeature[ScoutData.FeatureScoutDataField] = this;
        return true;
    }
}