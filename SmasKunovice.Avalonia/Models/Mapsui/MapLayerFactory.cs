using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.ArcGIS;
using Mapsui.ArcGIS.DynamicProvider;
using Mapsui.Cache;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public static class MapLayerFactory
{
    private const string ZtmBaseRestUrl = "https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/{{ZTM_DATASET}}/MapServer";
    public const string ProcedureLayerPrefix = "proc_";

    public static ImageLayer[] CreateZtmDynamicLayers(ZtmDatasets ztmDatasetFar, ZtmDatasets ztmDatasetNear)
    {
        var farLayer = CreateZtmLayer(ztmDatasetFar, minVisible: 8);
        var nearLayer = CreateZtmLayer(ztmDatasetNear, maxVisible: 8);
        return [farLayer, nearLayer];
    }

    private static ImageLayer CreateZtmLayer(ZtmDatasets ztmDataset, double minVisible = 0, double maxVisible = double.MaxValue)
    {
        var url = ZtmBaseRestUrl.Replace("{{ZTM_DATASET}}", ztmDataset.ToString());
        IUrlPersistentCache? defaultCache = null;
        var capabilitiesHelper = new CapabilitiesHelper(defaultCache);

        var capabilitiesTask = new TaskCompletionSource<ArcGISDynamicCapabilities>();
        capabilitiesHelper.CapabilitiesReceived += (sender, args) =>
        {
            if (sender is ArcGISDynamicCapabilities capabilities)
            {
                LogExtensions.LogInfo("Got capabilities", null);
                capabilitiesTask.TrySetResult(capabilities);
            }
            else
                capabilitiesTask.TrySetException(new InvalidOperationException("Failed to get valid capabilities"));
        };

        LogExtensions.LogInfo(url, null);
        capabilitiesHelper.GetCapabilities(url, CapabilitiesType.DynamicServiceCapabilities);

        _ = Task.WhenAny(capabilitiesTask.Task, Task.Delay(TimeSpan.FromSeconds(10))).Result;
        if (capabilitiesTask.Task.IsCompleted == false)
        {
            var ex = new TimeoutException("Timeout while getting capabilities");
            LogExtensions.LogFatal(ex, "Timeout while getting capabilities", null);
            throw ex;
        }

        // _urlDatasetCache[ztmDataset] = defaultCache!;
        var capabilities = capabilitiesTask.Task.Result;
        var provider = new ArcGISDynamicProvider(url, capabilities, null, defaultCache) { CRS = "EPSG:5514" };

        return new ImageLayer(ztmDataset.ToString())
        {
            Name = "ZTM layer",
            DataSource = provider,
            MinVisible = minVisible,
            MaxVisible = maxVisible
        };
    }

    public static UpdatingPositionLayer CreatePlanesPointLayer(IDronetagClient dronetagClient, AircraftDatabase aircraftDb, SvgStyleProvider svgStyleProvider, Map map)
    {
        var aircraftSymbolProvider = new AircraftSymbolProvider(svgStyleProvider);
        return new UpdatingPositionLayer(new DynamicScoutDataProvider(dronetagClient), aircraftDb, aircraftSymbolProvider, map)
        {
            Name = "Position layer",
        };
    }

    public static UpdatingTrajectoryLayer CreateTrajectoryLayer(IDronetagClient dronetagClient)
    {
        var style = new SymbolStyle
        {
            Fill = new Brush(Color.FromString("#c3fc05")),
            Outline = new Pen(Color.Black, 2),
            SymbolScale = 0.15f,
            SymbolType = SymbolType.Ellipse
        };

        return new UpdatingTrajectoryLayer(new DynamicScoutDataProvider(dronetagClient))
        {
            Name = "Trajectory layer",
            Style = style
        };
    }

    public static UpdatingSpeedVectorLayer CreateSpeedVectorLayer(IDronetagClient dronetagClient)
    {
        var style = new VectorStyle
        {
            Outline = new Pen(Color.Black, 2),
            Line = new Pen(Color.FromString("#c3fc05"), 2),
        };

        return new UpdatingSpeedVectorLayer(new DynamicScoutDataProvider(dronetagClient))
        {
            Name = "Speed vector layer",
            Style = style
        };
    }

    public static IEnumerable<ILayer> CreateAirportElementsLayers(GeoJsonLayerStyleProvider layerStyleProvider, out IReadOnlyList<string> procedureLayerNames)
    {
        var procedureLayerNamesList = new List<string>();
        var layers = layerStyleProvider.GeoJsonLayerProperties.OrderByDescending(layerConfig => layerConfig.Order).Select(
            layerConfig =>
            {
                var layer = new Layer
                {
                    DataSource = layerConfig.Provider,
                    Style = layerConfig.Style,
                    Opacity = layerConfig.Opacity,
                    Name = layerConfig.Name
                };

                if (!layerConfig.Name.StartsWith(ProcedureLayerPrefix)) 
                    return layer;
                
                procedureLayerNamesList.Add(layerConfig.Name[ProcedureLayerPrefix.Length..]);
                layer.Enabled = false;
                return layer;
            });
        procedureLayerNames = procedureLayerNamesList;
        return layers;
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