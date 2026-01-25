using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Mapsui.Nts;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// Provides style information for GeoJSON files by extracting color properties
/// </summary>
public class GeoJsonLayerStyleProvider
{
    private const string ColorPropertyName = "color";
    private const string OpacityPropertyName = "opacity";
    private const string OrderPropertyName = "order";
    private const string LabelsPropertyName = "drawLabels";
    private const string OutlinePropertyName = "outline";

    private readonly string _geoJsonBasePath;

    private readonly List<LayerProperty> _geoJsonLayerProperties = [];
    public IEnumerable<LayerProperty> GeoJsonLayerProperties => _geoJsonLayerProperties;
    private readonly Color _defaultColor = Color.Grey;
    public const float DefaultOpacity = 1.0f;
    public const int DefaultOrder = 5;

    /// <summary>
    /// Provides style information for GeoJSON files by extracting color properties
    /// </summary>
    public GeoJsonLayerStyleProvider(string geoJsonBasePath)
    {
        _geoJsonBasePath = geoJsonBasePath ?? throw new ArgumentNullException(nameof(geoJsonBasePath));
        Initialize();
    }


    /// <summary>
    /// Initializes the style provider by reading all GeoJSON files
    /// in the specified directory and extracting their color properties
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the GeoJSON base path doesn't exist</exception>
    private void Initialize()
    {
        try
        {
            if (!Directory.Exists(_geoJsonBasePath))
            {
                var ex = new DirectoryNotFoundException($"GeoJSON directory not found: {_geoJsonBasePath}");
                LogExtensions.LogError(ex, "GeoJSON directory not found", this);
                throw ex;
            }

            var geoJsonFiles = Directory.GetFiles(_geoJsonBasePath, "*.geojson", SearchOption.AllDirectories);
            LogExtensions.LogInfo("Found {0} GeoJSON files to process", this, geoJsonFiles.Length);

            foreach (var geoJsonFile in geoJsonFiles)
            {
                var success = TryGetDocument(geoJsonFile, out var document);
                if (!success)
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(geoJsonFile);

                var color = GetColorFromGeoJson(document!);
                var opacity = GetOpacityFromGeoJson(document!);
                var order = GetOrderFromGeoJson(document!);
                var drawLabels = GetDrawLabelsFromGeoJson(document!);
                var drawOutline = GetDrawOutlineFromGeoJson(document!);

                _geoJsonLayerProperties.Add(new LayerProperty
                {
                    Name = fileName,
                    Style = GetStyles(color, drawLabels, drawOutline),
                    Opacity = opacity,
                    Order = order,
                    Provider = new GeoJsonProvider(geoJsonFile),
                });

                document?.Dispose();
            }

            LogExtensions.LogInfo("Successfully initialized GeoJsonStyleProvider", this);
        }
        catch (Exception ex)
        {
            LogExtensions.LogError(ex, "Failed to initialize GeoJsonStyleProvider", this);
            throw;
        }
    }

    private bool TryGetDocument(string geoJsonPath, out JsonDocument? document)
    {
        var fileName = Path.GetFileNameWithoutExtension(geoJsonPath);
        var jsonContent = File.ReadAllText(geoJsonPath);
        document = null;

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            LogExtensions.LogWarning("Empty or whitespace-only content in file: {0}", this, fileName);
            return false;
        }

        try
        {
            document = JsonDocument.Parse(jsonContent);
        }
        catch (JsonException e)
        {
            LogExtensions.LogError(e, "Failed to parse file to JsonDocument: {0}", this, fileName);
            return false;
        }
        catch (Exception ex)
        {
            LogExtensions.LogError(ex, "Error processing file {0}", this, Path.GetFileName(geoJsonPath));
            return false;
        }

        return true;
    }

    private static StyleCollection GetStyles(Color color, bool createLabels, bool drawOutline)
    {
        var pointStyle = new ThemeStyle(feature =>
        {
            return feature switch
            {
                GeometryFeature { Geometry: Point } => new SymbolStyle()
                {
                    Fill = drawOutline ? null : new Brush(color),
                    SymbolScale = 0.3f,
                    SymbolType = SymbolType.Triangle,
                },
                _ => new VectorStyle
                {
                    Fill = drawOutline ? null : new Brush(color),
                    Line = new Pen(color)
                }
            };
        });
        var styles = new StyleCollection { Styles = [pointStyle] };
        if (!createLabels) return styles;

        var labelStyle = new ThemeStyle(feature => new LabelStyle
        {
            BackColor = null,
            ForeColor = Color.Azure,
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            Offset = new RelativeOffset(0, 1.9),
            Font = new Font
            {
                Size = 9,
                FontFamily = "Arial",
                // Bold = true,
            },
            Text = feature["label"]?.ToString() ?? "???"
        });
        styles.Styles.Add(labelStyle);

        return styles;
    }

    private static bool GetDrawLabelsFromGeoJson(JsonDocument document) => document.RootElement.TryGetProperty(LabelsPropertyName, out var hasLabels) && hasLabels.GetBoolean();
    private static bool GetDrawOutlineFromGeoJson(JsonDocument document) => document.RootElement.TryGetProperty(OutlinePropertyName, out var hasLabels) && hasLabels.GetBoolean();

    private Color GetColorFromGeoJson(JsonDocument document)
    {
        var colorValue = document.RootElement.TryGetProperty(ColorPropertyName, out var directColor)
            ? directColor.GetString()
            : null;

        if (colorValue is null)
            return _defaultColor;

        Color? color = null;
        try
        {
            color = Color.FromString(colorValue);
        }
        catch (Exception e)
        {
            LogExtensions.LogWarning("Failed to parse color '{0}' from document", this, colorValue);
        }

        return color ?? _defaultColor;
    }

    private static float GetOpacityFromGeoJson(JsonDocument document)
    {
        try
        {
            if (document.RootElement.TryGetProperty(OpacityPropertyName, out var directOpacity) &&
                directOpacity.TryGetSingle(out var opacityValue))
            {
                return Math.Clamp(opacityValue, 0, 1);
            }
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Failed to parse opacity from document");
        }

        return DefaultOpacity;
    }

    private int GetOrderFromGeoJson(JsonDocument document)
    {
        try
        {
            if (document.RootElement.TryGetProperty(OrderPropertyName, out var directOrder) &&
                directOrder.TryGetInt32(out var orderValue))
            {
                LogExtensions.LogDebug("Extracted order '{0}' from document", this, orderValue);
                return orderValue;
            }
        }
        catch (InvalidOperationException e)
        {
            LogExtensions.LogError(e, "Failed to parse order from document");
        }

        return DefaultOrder;
    }
}

public record LayerProperty
{
    public required string Name { get; init; }
    public required IStyle Style { get; init; }
    public required int Order { get; init; }
    public required GeoJsonProvider Provider { get; init; }

    private readonly float _opacity;

    public required float Opacity
    {
        get => Math.Clamp(_opacity, 0, 1);
        init => _opacity = value;
    }
}