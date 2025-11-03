using System;
using System.Collections.Generic;
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

    private readonly string _geoJsonBasePath;

    private readonly List<LayerProperty> _geoJsonLayerProperties = [];
    public IEnumerable<LayerProperty> GeoJsonLayerProperties => _geoJsonLayerProperties;
    private readonly Color _defaultColor = Color.Grey;

    /// <summary>
    /// Provides style information for GeoJSON files by extracting color properties
    /// </summary>
    public GeoJsonLayerStyleProvider(string geoJsonBasePath)
    {
        _geoJsonBasePath = geoJsonBasePath ?? throw new ArgumentNullException(nameof(geoJsonBasePath));
        Initialize();
    }

    public const float DefaultOpacity = 1.0f;
    public const int DefaultOrder = 5;

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

                Color color;
                float opacity;
                int order;

                try
                {
                    color = GetColorFromGeoJson(document!);
                    opacity = GetOpacityFromGeoJson(document!);
                    order = GetOrderFromGeoJson(document!);
                }
                catch (Exception e)
                {
                    LogExtensions.LogError(e, "Parsing of file {0} has failed", this, Path.GetFileName(geoJsonFile));
                    throw;
                }

                _geoJsonLayerProperties.Add(new LayerProperty
                {
                    Name = fileName,
                    Style = GetStyle(color),
                    Opacity = opacity,
                    Order = order,
                    Provider = new GeoJsonProvider(geoJsonFile)
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
            LogExtensions.LogWarning("Failed to parse file to JsonDocument: {0}", this, fileName);
            return false;
        }
        catch (Exception ex)
        {
            LogExtensions.LogError(ex, "Error processing file {0}", this, Path.GetFileName(geoJsonPath));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Retrieves a style for the specified file name, using the associated color from the color map or default color
    /// </summary>
    /// <param name="fileName">The name of the file to get the style for</param>
    /// <param name="color"></param>
    /// <returns>A ThemeStyle that applies different styling based on geometry type (Point vs other geometries)</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not initialized</exception>
    private static IStyle GetStyle(Color color)
    {
        return new ThemeStyle(feature =>
        {
            return feature switch
            {
                GeometryFeature { Geometry: Point } => new SymbolStyle()
                {
                    Fill = new Brush(color),
                    SymbolScale = 0.2f,
                },
                _ => new VectorStyle
                {
                    Fill = new Brush(color),
                    Line = new Pen(color)
                }
            };
        });
    }


    private Color GetColorFromGeoJson(JsonDocument document)
    {
        var colorValue = document.RootElement.TryGetProperty(ColorPropertyName, out var directColor)
            ? directColor.GetString()
            : null;

        if (colorValue is not null)
        {
            Color? color = null;
            try
            {
                color = Color.FromString(colorValue);
            }
            catch (ArgumentException e)
            {
                LogExtensions.LogDebug("Failed to parse color '{0}' from document", this, colorValue);
            }

            if (color is null)
            {
                LogExtensions.LogDebug("Failed to parse color '{0}' from document", this, colorValue);
                return _defaultColor;
            }

            LogExtensions.LogDebug("Extracted color '{0}' from document", this, colorValue);
            return color;
        }

        LogExtensions.LogDebug("No color property found in document", this);

        return _defaultColor;
    }

    private float GetOpacityFromGeoJson(JsonDocument document)
    {
        try
        {
            if (document.RootElement.TryGetProperty(OpacityPropertyName, out var directOpacity) &&
                directOpacity.TryGetSingle(out var opacityValue))
            {
                LogExtensions.LogDebug("Extracted opacity '{0}' from document", this, opacityValue);
                return Math.Clamp(opacityValue, 0, 1);
            }
        }
        catch (InvalidOperationException e)
        {
            LogExtensions.LogError(e, "Failed to parse opacity from document");
            throw;
        }

        LogExtensions.LogDebug("No opacity property found in document", this);

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
            throw;
        }

        LogExtensions.LogDebug("No order property found in document", this);

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