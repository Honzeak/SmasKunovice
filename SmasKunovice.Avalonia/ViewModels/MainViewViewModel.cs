using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Layers.AnimatedLayers;
using Mapsui.Nts;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling.Layers;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty] private Map _map = new();
    private string _svgBasePath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\Svg\";
    private readonly IDronetagClient? _dronetagClient;
    public bool HasClient => _dronetagClient is not null;
    
    public MainViewViewModel() {}

    public MainViewViewModel(IDronetagClient dronetagClient)
    {
        _dronetagClient = dronetagClient;
    }

    public Map CreateMap()
    {
        var map = new Map();
        try
        {
            map.CRS = "EPSG:5514";
            map.Layers.Add(ZtmDynamicLayerFactory.CreateDynamicLayer(ZtmDatasets.ZTM100));
            map.Layers.Add(CreateAirportElementsLayers());
            if (HasClient)
                map.Layers.Add(CreatePlanesAnimatedPointLayer(_dronetagClient!));
            else 
                Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, "{0} not provided. Creating map without SMAS data.", nameof(IDronetagClient));
            
            map.Navigator.CenterOnAndZoomTo(new MPoint(-539192.3d, -1184647.4d), 900);
        }
        catch (Exception e)
        {
            Greeting = e.Message;
            throw;
        }

        return map;
    }

    private ILayer CreatePlanesAnimatedPointLayer(IDronetagClient dronetagClient)
    {
        var svgStyleProvider = new SvgStyleProvider(_svgBasePath);
        var airplaneId = svgStyleProvider.RegisterSvg("airplane.svg");
        var themeStyle = new ThemeStyle(feature =>
        {
            return new SymbolStyle
            {
                BitmapId = airplaneId,
                SymbolScale = .03f,
                SymbolRotation = feature["ScoutData"] switch
                {
                    ScoutData { Odid.Location.Direction: not null } message => (double)message.Odid.Location.Direction,
                    _ => 0
                }
            };
        });

        return new AnimatedPointLayer(new DynamicScoutDataProvider(dronetagClient)){ Style = themeStyle };
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
        GeometryFeature { Geometry: Point } => new SymbolStyle()
        {
            Fill = new Brush(new Color(245, 245, 242)),
            SymbolScale = 0.3f
        },
        _ => new VectorStyle
        {
            Fill = new Brush(new Color(200, 200, 200)),
        }
    };

    [Obsolete("Agreed to use ARCGis dynamic tiling")]
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
}