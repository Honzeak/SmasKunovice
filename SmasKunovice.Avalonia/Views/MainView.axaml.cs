using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using BruTile;
using BruTile.MbTiles;
using Mapsui;
using Mapsui.ArcGIS;
using Mapsui.ArcGIS.DynamicProvider;
using Mapsui.Cache;
using Mapsui.Extensions.Cache;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using SmasKunovice.Avalonia.ViewModels;
using SQLite;
using Brush = Mapsui.Styles.Brush;
using Color = Mapsui.Styles.Color;
using Pen = Mapsui.Styles.Pen;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewViewModel();
        MapControl.Map = CreateMap();
    }

    private Map CreateMap()
    {
        var map = new Map();
        map.CRS = "EPSG:5514";
        map.Layers.Add(CreateDynamicLayer());
        // Debugger.Launch();
        map.Layers.Add(CreateAirportElementsLayers());
        return map;
    }

    private ImageLayer CreateDynamicLayer()
    {
        IUrlPersistentCache? defaultCache = null;
        var url = @"https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/ZTM100/MapServer";
        var capabilitiesHelper = new CapabilitiesHelper(defaultCache);
        ArcGISDynamicCapabilities? capabilities = null;
        capabilitiesHelper.CapabilitiesReceived += (sender, args) => capabilities = sender as ArcGISDynamicCapabilities;
        capabilitiesHelper.GetCapabilities(url, CapabilitiesType.DynamicServiceCapabilities);
        while (capabilities == null)
        {
            Task.Delay(100).ConfigureAwait(false);
        }
        
        var provider = new ArcGISDynamicProvider(url, capabilities, null, defaultCache){CRS = "EPSG:5514"};
        return new ImageLayer { DataSource = provider };
    }

    private ILayer[] CreateAirportElementsLayers()
    {
        var geoJsonsDirPath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\GeoJsonElements\";

        List<ILayer> layers = [];
        ILayer? runwayMarkingsLayer = null;
        foreach (var path in Directory.GetFiles(geoJsonsDirPath, "*.geojson"))
        {
            var isRunwayMarkings = path.Contains("RunwayMarkings");
            var geoJsonProvider = new GeoJsonProvider(path);

            var layer = new Layer()
            {
                DataSource = geoJsonProvider,
                Style = isRunwayMarkings ? new VectorStyle { Fill = new Brush(Color.White) } : CreateThemeStyle()
            };
            if (isRunwayMarkings)
            {
                runwayMarkingsLayer = layer;
                continue;
            }

            layers.Add(layer);
        }

        if (runwayMarkingsLayer != null)
            layers.Add(runwayMarkingsLayer);

        return layers.ToArray();
    }

    // je to na picu, prepsat
    private ThemeStyle CreateThemeStyle()
    {
        return new ThemeStyle(CreateVectorStyle);
    }

    private VectorStyle CreateVectorStyle(IFeature feature) => feature switch
    {
        GeometryFeature { Geometry: NetTopologySuite.Geometries.Point } => new SymbolStyle()
        {
            Fill = new Brush(new Color(245, 245, 242)),
            SymbolScale = 0.3f
        },
        _ => new VectorStyle
        {
            Fill = new Brush(new Color(200, 200, 200)),
        }
    };

    private static ILayer CreateGeoTiffLayer()
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

    // public static class GeoJsonAirportStyleProvider
    // {
    //     public static VectorStyle GetStyle(GeoJsonProvider provider)
    //     {
    //         var extent = provider.GetExtent(
    //     }
}