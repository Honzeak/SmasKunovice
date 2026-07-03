using System;
using Mapsui.Styles;
using SmasKunovice.Avalonia.Models.Dronetag;
using MapsuiColor = Mapsui.Styles.Color;
using SystemColor = System.Drawing.Color;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class TargetStyleBuilder(SvgStyleProvider svgStyleProvider)
{
    private const string SvgSymbolFileName = "hexagon-full.svg";
    private readonly int _hexagonIdDefault = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Blue, SystemColor.Blue);
    private readonly int _hexagonIdSelected = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.Yellow, SystemColor.Yellow);
    private readonly int _hexagonIdStale = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.WhiteSmoke, SystemColor.WhiteSmoke);
    // private readonly int _hexagonIdBelowLimit = svgStyleProvider.RegisterSvg(SvgSymbolFileName, SystemColor.MediumVioletRed, SystemColor.MediumVioletRed);
    
    private SymbolStyle _symbolStyle = new();
    private ScoutData? _scoutData;
    private readonly Brush _defaultBrush = new (MapsuiColor.Green);
    private readonly Brush _selectedBrush = new (MapsuiColor.Yellow);
    private readonly Brush _staleBrush = new (MapsuiColor.WhiteSmoke);

    public TargetStyleBuilder Initialize(ScoutData scoutData)
    {
        _scoutData = scoutData;
        _symbolStyle = new SymbolStyle
        {
            SymbolScale = 0.25f,
            Fill = _defaultBrush
        };
        
        ConfigureSymbolType();
        return this;
    }
    
    private void ConfigureSymbolType()
    {
        if (_scoutData!.IsDrone())
        {
            _symbolStyle.SymbolType = SymbolType.Image;
            _symbolStyle.BitmapId = _hexagonIdDefault;
            _symbolStyle.SymbolScale = 0.015f;
        }
        else if (_scoutData.IsVehicle())
            _symbolStyle.SymbolType = SymbolType.Ellipse;
        else
            _symbolStyle.SymbolType = SymbolType.Rectangle;
    }

    public SymbolStyle Build()
    {
        CheckInit();
        _scoutData = null;
        return _symbolStyle;
    }

    private void CheckInit()
    {
        if (_scoutData is null)
            throw new InvalidOperationException("Initialize() must be called before using the builder.");
    }

    public TargetStyleBuilder WithSelected()
    {
        CheckInit();
        _symbolStyle.SymbolScale = _scoutData!.IsDrone() ? 0.025f : 0.4f;
        if (_scoutData.IsDrone())
            _symbolStyle.BitmapId = _hexagonIdSelected;
        else 
            _symbolStyle.Fill = _selectedBrush;
        
        return this;
    }
    
    public TargetStyleBuilder WithStale()
    {
        CheckInit();
        if (_scoutData!.IsDrone())
            _symbolStyle.BitmapId = _hexagonIdStale;
        else 
            _symbolStyle.Fill = _staleBrush;

        return this;
    }
}