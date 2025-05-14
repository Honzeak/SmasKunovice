using System;
using System.Collections.Generic;
using System.IO;
using Mapsui.Styles;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace SmasKunovice.Avalonia.Models;

public class SvgStyleProvider
{
    private readonly string _basePath;

    public SvgStyleProvider(string basePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePath);
        _basePath = basePath;
    }

    public int RegisterSvg(string svgFileName, System.Drawing.Color? fillColor = null,
        System.Drawing.Color? strokeColor = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(svgFileName);
        if (!svgFileName.EndsWith(".svg"))
            svgFileName += ".svg";
        var svgFilePath = Path.Combine(_basePath, svgFileName);

        if (!File.Exists(svgFilePath))
            throw new FileNotFoundException(svgFileName);

        using var stream = File.OpenRead(svgFilePath);
        var skPicture = ToSkPicture(stream, fillColor, strokeColor) ?? throw new FormatException("Failed to parse SVG");
        return BitmapRegistry.Instance.Register(skPicture);
    }

    private static SKPicture? ToSkPicture(Stream stream, System.Drawing.Color? fillColor,
        System.Drawing.Color? strokeColor)
    {
        var svgDocument = SvgExtensions.Open(stream);
        if (svgDocument is null) return null;

        foreach (var element in GetAllElements(svgDocument.Children))
        {
            if (element.Fill is not null && fillColor is { })
                element.Fill = new SvgColourServer(fillColor.Value);
            if (element.Stroke is not null && strokeColor is { })
                element.Stroke = new SvgColourServer(strokeColor.Value);
        }

        var skiaModel = new SkiaModel(new SKSvgSettings());
        var assetLoader = new SkiaAssetLoader(skiaModel);
        var model = SvgExtensions.ToModel(svgDocument, assetLoader, out var _, out _);
        return skiaModel.ToSKPicture(model);
    }

    private static List<SvgElement> GetAllElements(SvgElementCollection elements)
    {
        var result = new List<SvgElement>();
        foreach (var element in elements)
        {
            result.Add(element);

            if (element.Children.Count > 0)
                result.AddRange(GetAllElements(element.Children));
        }

        return result;
    }
}