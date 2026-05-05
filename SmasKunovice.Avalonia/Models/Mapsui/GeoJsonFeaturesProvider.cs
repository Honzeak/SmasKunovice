using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts.Providers;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class GeoJsonFeaturesProvider
{
    public IEnumerable<IFeature> Features { get; }
    public GeoJsonFeaturesProvider(string geoJsonPath)
    {
        if (!File.Exists(geoJsonPath))
            throw new FileNotFoundException("GeoJson file not found", geoJsonPath);

        var extent = GetExtentFromJson(geoJsonPath);
        var fetchInfo = new FetchInfo(new MSection(new MRect(extent[0], extent[1], extent[2], extent[3]), 1));
        var geoJsonProvider = new GeoJsonProvider(geoJsonPath);
        Features = geoJsonProvider.GetFeaturesAsync(fetchInfo).GetAwaiter().GetResult().ToList();
    }


    private static List<int> GetExtentFromJson(string geoJsonPath)
    {
        var json = File.ReadAllText(geoJsonPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("extent", out var extentElement))
            throw new InvalidOperationException("The GeoJSON file does not contain a root 'extent' property.");

        if (extentElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The root 'extent' property is not an array.");

        var extent = new List<int>();

        foreach (var item in extentElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value))
                throw new InvalidOperationException("The 'extent' array must contain only integers.");

            extent.Add(value);
        }

        if (extent.Count != 4)
            throw new InvalidOperationException($"Expected 4 integers in 'extent', but found {extent.Count}.");

        return extent;
    }
}