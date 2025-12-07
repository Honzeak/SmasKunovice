using Mapsui.Styles;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.TestUtils;

public class DummyAircraftSymbolProvider : IAircraftSymbolProvider
{
    private static SymbolStyle GetBaseStyle() => new()
    {
        SymbolScale = 0.2f,
        Outline = new Pen(Color.Black, 3),
        Fill = new Brush(Color.FromString("#c3fc05"))
    };

    public IStyle GetAirplaneStyle(bool selected) => GetBaseStyle();

    public IStyle GetVehicleStyle(bool selected) => GetBaseStyle();

    public IStyle GetDroneStyle(bool selected) => GetBaseStyle();
}