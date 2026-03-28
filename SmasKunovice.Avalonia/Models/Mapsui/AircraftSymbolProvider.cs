using System;
using Mapsui.Styles;
using MapsuiColor = Mapsui.Styles.Color;
using SystemColor = System.Drawing.Color;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public interface IAircraftSymbolProvider
{
    IStyle GetAirplaneStyle(SymbolState state);
    IStyle GetVehicleStyle(SymbolState state);
    IStyle GetDroneStyle(SymbolState state);
}
public enum SymbolState {Normal, Selected, Stale}
public class AircraftSymbolProvider(SvgStyleProvider svgStyleProvider) : IAircraftSymbolProvider
{
    private readonly int _hexagonIdNormal = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Blue, SystemColor.Blue);
    private readonly int _hexagonIdSelected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Yellow, SystemColor.Yellow);
    private readonly int _hexagonIdStale = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.WhiteSmoke, SystemColor.WhiteSmoke);
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

    private static SymbolStyle GetStaleStyle() => new()
    {
        SymbolScale = 0.25f,
        Fill = new Brush(MapsuiColor.WhiteSmoke)
    };

    public IStyle GetAirplaneStyle(SymbolState state)
    {
        var style = GetStateStyle(state);
        style.SymbolType = SymbolType.Rectangle;
        return style;
    }

    private static SymbolStyle GetStateStyle(SymbolState state)
    {
        var style = state switch
        {
            SymbolState.Normal => GetBaseStyle(),
            SymbolState.Selected => GetBaseStyleSelected(),
            SymbolState.Stale => GetStaleStyle(),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
        return style;
    }

    public IStyle GetVehicleStyle(SymbolState state)
    {
        var style = GetStateStyle(state);
        style.SymbolType = SymbolType.Ellipse;
        return style;
    }

    public IStyle GetDroneStyle(SymbolState state)
    {
        var style = GetStateStyle(state);
        style.SymbolType = SymbolType.Image;
        style.BitmapId = state switch
        {
            SymbolState.Normal => _hexagonIdNormal,
            SymbolState.Selected => _hexagonIdSelected,
            SymbolState.Stale => _hexagonIdStale,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
        style.SymbolScale = state == SymbolState.Selected ? 0.025f : 0.015f;
        return style;
    }
}