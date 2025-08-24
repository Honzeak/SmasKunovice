using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Logging;
using Mapsui.ArcGIS;
using Mapsui.ArcGIS.DynamicProvider;
using Mapsui.Cache;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Layers.AnimatedLayers;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling.Layers;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class MapLayerFactory(string svgBasePath, string geoJsonsBasePath)
{
    private const string ZtmBaseRestUrl = "https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/{{ZTM_DATASET}}/MapServer";
    private GeoJsonStyleProvider _styleProvider = new (geoJsonsBasePath);

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
                Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, "Got capabilities");
                capabilitiesTask.TrySetResult(capabilities);
            }
            else
                capabilitiesTask.TrySetException(new InvalidOperationException("Failed to get valid capabilities"));
        };

        Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, this, url);
        capabilitiesHelper.GetCapabilities(url, CapabilitiesType.DynamicServiceCapabilities);

        _ = Task.WhenAny(capabilitiesTask.Task, Task.Delay(TimeSpan.FromSeconds(10))).Result;
        if (capabilitiesTask.Task.IsCompleted == false)
        {
            Logger.Sink?.Log(LogEventLevel.Fatal, LogArea.Control, this, "Timeout while getting capabilities");
            throw new TimeoutException("Timeout while getting capabilities");
        }

        // _urlDatasetCache[ztmDataset] = defaultCache!;
        var capabilities = capabilitiesTask.Task.Result;
        var provider = new ArcGISDynamicProvider(url, capabilities, null, defaultCache) { CRS = "EPSG:5514" };

        return new ImageLayer(ztmDataset.ToString()) { DataSource = provider };
    }

    public ILayer CreatePlanesAnimatedPointLayer(IDronetagClient dronetagClient)
    {
        var svgStyleProvider = new SvgStyleProvider(svgBasePath);
        var airplaneId = svgStyleProvider.RegisterSvg("airplane.svg");
        var themeStyle = new ThemeStyle(feature =>
        {
            return new SymbolStyle
            {
                BitmapId = airplaneId,
                SymbolScale = .03f,
                SymbolRotation = feature["ScoutData"] switch
                {
                    ScoutData { Odid.Location.Direction: not null } message => (double)message.Odid.Location
                        .Direction,
                    _ => 0
                }
            };
        });

        return new AnimatedPointLayer(new DynamicScoutDataProvider(dronetagClient)) { Style = themeStyle };
    }

    public ILayer[] CreateAirportElementsLayers()
    {
        if (!_styleProvider.IsInitialized)
            _styleProvider.Initialize();
        
        List<ILayer> layers = [];
        ILayer? runwayMarkingsLayer = null;
        foreach (var path in Directory.GetFiles(geoJsonsBasePath, "*.geojson"))
        {
            var isRunwayMarkings = path.Contains("RunwayMarkings");
            var geoJsonProvider = new GeoJsonProvider(path);

            var layer = new Layer()
            {
                DataSource = geoJsonProvider,
                Style = _styleProvider.GetStyle(Path.GetFileNameWithoutExtension(path))
            };
            if (isRunwayMarkings)
            {
                runwayMarkingsLayer = layer;
                continue;
            }

            // TODO sort layers in a different way
            layers.Add(layer);
        }

        // Last layer to add is the top layer
        if (runwayMarkingsLayer != null)
            layers.Add(runwayMarkingsLayer);

        return layers.ToArray();
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
