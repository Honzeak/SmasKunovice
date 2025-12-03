using Mapsui.Styles;
using MapsuiColor = Mapsui.Styles.Color;
using SystemColor = System.Drawing.Color;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public interface IAircraftSymbolProvider
{
    SymbolStyle GetAirplaneStyle();
    SymbolStyle GetVehicleStyle();
    SymbolStyle GetDroneStyle();
}
public class AircraftSymbolProvider(SvgStyleProvider svgStyleProvider) : IAircraftSymbolProvider
{
    private readonly int _hexagonIdUnselected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Brown, SystemColor.Brown);
    private readonly int _hexagonIdSelected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Green, SystemColor.Green);
    private const string SvgSymbolFileName = "hexagon-full.svg";

    private static SymbolStyle GetBaseStyle() => new()
    {
        SymbolScale = 0.2f,
        Outline = new Pen(MapsuiColor.Black, 3),
        Fill = new Brush(MapsuiColor.Green)
    };

    public SymbolStyle GetAirplaneStyle()
    {
        var airplaneStyle = GetBaseStyle();
        airplaneStyle.SymbolType = SymbolType.Rectangle;
        return airplaneStyle;
    }

    public SymbolStyle GetVehicleStyle()
    {
        var style = GetBaseStyle();
        style.SymbolType = SymbolType.Ellipse;
        return style;
    }

    public SymbolStyle GetDroneStyle()
    {
        var style = GetBaseStyle();
        style.SymbolType = SymbolType.Image;
        style.BitmapId = _hexagonIdUnselected;
        style.SymbolScale = 0.015f;
        return style;
    }
}