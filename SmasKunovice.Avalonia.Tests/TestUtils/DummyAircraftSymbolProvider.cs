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

    public SymbolStyle GetAirplaneStyle() => GetBaseStyle();

    public SymbolStyle GetVehicleStyle() => GetBaseStyle();

    public SymbolStyle GetDroneStyle() => GetBaseStyle();
}