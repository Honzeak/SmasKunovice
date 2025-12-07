using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using MapsuiColor = Mapsui.Styles.Color;
using SystemColor = System.Drawing.Color;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public interface IAircraftSymbolProvider
{
    IStyle GetAirplaneStyle(bool selected);
    IStyle GetVehicleStyle(bool selected);
    IStyle GetDroneStyle(bool selected);
}
public class AircraftSymbolProvider(SvgStyleProvider svgStyleProvider) : IAircraftSymbolProvider
{
    private readonly int _hexagonIdUnselected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Blue, SystemColor.Blue);
    private readonly int _hexagonIdSelected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Yellow, SystemColor.Yellow);
    private const string SvgSymbolFileName = "hexagon-full.svg";

    private static SymbolStyle GetBaseStyle() => new()
    {
        SymbolScale = 0.25f,
        // Outline = new Pen(MapsuiColor.Black, 3),
        Fill = new Brush(MapsuiColor.Green)
    };

    private static SymbolStyle GetBaseStyleSelected() => new()
    {
        SymbolScale = 0.4f,
        Fill = new Brush(MapsuiColor.Yellow)
    };

    public IStyle GetAirplaneStyle(bool selected)
    {
        var style = selected ? GetBaseStyleSelected() : GetBaseStyle();
        style.SymbolType = SymbolType.Rectangle;
        return style;
    }

    public IStyle GetVehicleStyle(bool selected)
    {
        var style = selected ? GetBaseStyleSelected() : GetBaseStyle();
        style.SymbolType = SymbolType.Ellipse;
        return style;
    }

    public IStyle GetDroneStyle(bool selected)
    {
        var style = selected ? GetBaseStyleSelected() : GetBaseStyle();
        style.SymbolType = SymbolType.Image;
        style.BitmapId = selected ? _hexagonIdSelected : _hexagonIdUnselected;
        style.SymbolScale = selected ? 0.025f : 0.015f;
        return style;
    }
}