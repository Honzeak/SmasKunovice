using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mapsui.ArcGIS;
using Mapsui.ArcGIS.DynamicProvider;
using Mapsui.Cache;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class MapLayerFactory(string geoJsonsBasePath)
{
    private const string ZtmBaseRestUrl = "https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/{{ZTM_DATASET}}/MapServer";
    private readonly GeoJsonLayerStyleProvider _layerStyleProvider = new(geoJsonsBasePath);

    public ImageLayer CreateZtmDynamicLayer(ZtmDatasets ztmDataset)
    {
        var url = ZtmBaseRestUrl.Replace("{{ZTM_DATASET}}", ztmDataset.ToString());
        IUrlPersistentCache? defaultCache = null;
        var capabilitiesHelper = new CapabilitiesHelper(defaultCache);

        var capabilitiesTask = new TaskCompletionSource<ArcGISDynamicCapabilities>();
        capabilitiesHelper.CapabilitiesReceived += (sender, args) =>
        {
            if (sender is ArcGISDynamicCapabilities capabilities)
            {
                LogExtensions.LogInfo("Got capabilities", this);
                capabilitiesTask.TrySetResult(capabilities);
            }
            else
                capabilitiesTask.TrySetException(new InvalidOperationException("Failed to get valid capabilities"));
        };

        LogExtensions.LogInfo(url, this);
        capabilitiesHelper.GetCapabilities(url, CapabilitiesType.DynamicServiceCapabilities);

        _ = Task.WhenAny(capabilitiesTask.Task, Task.Delay(TimeSpan.FromSeconds(10))).Result;
        if (capabilitiesTask.Task.IsCompleted == false)
        {
            var ex = new TimeoutException("Timeout while getting capabilities");
            LogExtensions.LogFatal(ex, "Timeout while getting capabilities", this);
            throw ex;
        }

        // _urlDatasetCache[ztmDataset] = defaultCache!;
        var capabilities = capabilitiesTask.Task.Result;
        var provider = new ArcGISDynamicProvider(url, capabilities, null, defaultCache) { CRS = "EPSG:5514" };

        return new ImageLayer(ztmDataset.ToString()) { DataSource = provider };
    }

    public ILayer CreatePlanesPointLayer(IDronetagClient dronetagClient)
    {
        var style = new SymbolStyle
        {
            Fill = new Brush(Color.Red),
            Outline = new Pen(Color.Black, 2),
            SymbolScale = 0.2f,
            SymbolType = SymbolType.Rectangle
        };

        return new UpdatingPositionLayer(new DynamicScoutDataProvider(dronetagClient))
        {
            Style = style
        };
    }

    public ILayer[] CreateAirportElementsLayers()
    {
        if (!_layerStyleProvider.IsInitialized)
            _layerStyleProvider.Initialize();

        var layersWithOrder = new List<(ILayer layer, int order)>();

        foreach (var path in Directory.GetFiles(geoJsonsBasePath, "*.geojson"))
        {
            var geoJsonProvider = new GeoJsonProvider(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!_layerStyleProvider.LayerProperties.TryGetValue(fileName, out var props))
            {
                LogExtensions.LogError("Could not find properties for geoJson file {0}, skipping layer", fileName);
                continue;
            }

            var layer = new Layer
            {
                DataSource = geoJsonProvider,
                Style = GeoJsonLayerStyleProvider.GetStyle(props.Color),
                Opacity = props.Opacity,
                Name = props.Name
            };

            layersWithOrder.Add((layer, props.Order));
        }

        // Sort layers by Order value
        var sortedLayers = layersWithOrder
            .OrderByDescending(x => x.order)
            .Select(x => x.layer)
            .ToArray();

        return sortedLayers;
    }

    [Obsolete("Agreed to use ARCGis dynamic tiling")]
    public static ILayer CreateGeoTiffLayer()
    {
        // var MbTilesFilePath = @"C:\Users\honza\OneDrive\Code\SMAS-Data\Kunovice_tiff\mbTiles\output_file.mbtiles";
        // var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(MbTilesFilePath, true));
        // var mbTilesLayer = new TileLayer(mbTilesTileSource) { Name = "kundovice" };
        // return mbTilesLayer;
        var geotif = new GeoTiffProvider(@"C:\Users\honza\OneDrive\Code\SMAS-Data\Kunovice_tiff\0708D.tif",
            new List<Color>()
            {
                Color.Aqua
            });
        var gifLayer = new Layer("nig") { DataSource = geotif };
        var layer = new RasterizingTileLayer(gifLayer);
        return gifLayer;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum ZtmDatasets
{
    ZTM5,
    ZTM10,
    ZTM25,
    ZTM50,
    ZTM100
}