using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia.Logging;
using Mapsui.Nts;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using NetTopologySuite.Geometries;

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// Provides style information for GeoJSON files by extracting color properties
/// </summary>
public class GeoJsonStyleProvider(string geoJsonBasePath)
{
    private readonly string _geoJsonBasePath =
        geoJsonBasePath ?? throw new ArgumentNullException(nameof(geoJsonBasePath));

    private readonly Dictionary<string, Color> _colorMap = new();
    private readonly Color _defaultColor = new (255, 200, 200);

    /// <summary>
    /// Initializes the style provider by reading all GeoJSON files
    /// in the specified directory and extracting their color properties
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the GeoJSON base path doesn't exist</exception>
    public void Initialize()
    {
        try
        {
            if (!Directory.Exists(_geoJsonBasePath))
            {
                Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, this,
                    "GeoJSON directory not found: {Path}", _geoJsonBasePath);
                throw new DirectoryNotFoundException($"GeoJSON directory not found: {_geoJsonBasePath}");
            }

            var geoJsonFiles = Directory.GetFiles(_geoJsonBasePath, "*.geojson", SearchOption.AllDirectories);
            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Found {Count} GeoJSON files to process",
                geoJsonFiles.Length);
            foreach (var geoJsonFile in geoJsonFiles)
            {
                ProcessGeoJsonFile(geoJsonFile);
            }

            Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this,
                "Successfully initialized GeoJsonStyleProvider");
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control,
                "Failed to initialize GeoJsonStyleProvider: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a style for the specified file name, using the associated color from the color map or default color
    /// </summary>
    /// <param name="fileName">The name of the file to get the style for</param>
    /// <returns>A ThemeStyle that applies different styling based on geometry type (Point vs other geometries)</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not initialized</exception>
    public IStyle GetStyle(string fileName)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("GeoJsonStyleProvider is not initialized");

        Color color;
        lock (_colorMap)
        {
            color = _colorMap.GetValueOrDefault(fileName) ?? _defaultColor;
        }

        return new ThemeStyle(feature =>
        {
            return feature switch
            {
                GeometryFeature { Geometry: Point } => new SymbolStyle()
                {
                    Fill = new Brush(color),
                    SymbolScale = 0.3f
                },
                _ => new VectorStyle
                {
                    Fill = new Brush(color),
                }
            };
        });
    }

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Processes a single GeoJSON file to extract color information
    /// </summary>
    /// <param name="filePath">The path to the GeoJSON file</param>
    private void ProcessGeoJsonFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var jsonContent = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                Logger.Sink?.Log(LogEventLevel.Warning, LogArea.Control, this,
                    "Empty or whitespace-only content in file: {FileName}", fileName);
                return;
            }

            using var document = JsonDocument.Parse(jsonContent);
            var colorValue = document.RootElement.TryGetProperty("color", out var directColor)
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
                    Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this,
                        "Failed to parse color '{ColorValue}' from file: {FileName}", colorValue, fileName);
                }

                if (color is null)
                    return;
                
                lock (_colorMap)
                {
                    _colorMap[fileName] = color;
                }

                Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this,
                    "Extracted color '{Color}' from file: {FileName}", colorValue, fileName);
            }
            else
            {
                Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, this,
                    "No color property found in file: {FileName}", fileName);
            }
        }
        catch (JsonException ex)
        {
            Logger.Sink?.Log(LogEventLevel.Warning, LogArea.Control, this,
                "Invalid JSON in file {FileName}: {Error}", Path.GetFileName(filePath), ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, this,
                "Error processing file {FileName}: {Error}", Path.GetFileName(filePath), ex.Message);
        }
    }
}