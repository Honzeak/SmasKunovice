using Mapsui.Layers;

namespace SmasKunovice.Avalonia.Models;

public record DroneTagMessage(
    string Id,
    double Latitude,
    double Longitude,
    double Altitude,
    double Speed,
    double Heading)
{
    public PointFeature ToPointFeature()
    {
        var pointFeature = new PointFeature(Latitude, Longitude);
        pointFeature["ID"] = Id;
        pointFeature["Message"] = this;
        return pointFeature;
    }
}