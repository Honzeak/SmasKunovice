using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Microsoft.Extensions.Options;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Config;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel() : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty] private Map _map = new();
    [ObservableProperty] private int _trajectoryPointsCount;
    [ObservableProperty] private int _speedVectorMinuteInterval;
    [ObservableProperty] private bool _drawZtmMap = true;

    private readonly IDronetagClient? _dronetagClient;
    private readonly GeoJsonLayerStyleProvider? _layerStyleProvider;
    private readonly AircraftDatabase? _aircraftDatabase;
    private readonly SvgStyleProvider _svgStyleProvider;
    private bool HasClient => _dronetagClient is not null;

    public MainViewViewModel(IDronetagClient dronetagClient, IOptions<ApplicationSettings> options) : this()
    {
        _dronetagClient = dronetagClient;
        var appSettings = options.Value;
        _layerStyleProvider = new GeoJsonLayerStyleProvider(appSettings.GeoJsonsBasePath);
        _aircraftDatabase = new AircraftDatabase(appSettings.AircraftDatabasePath);
        _svgStyleProvider = new SvgStyleProvider(appSettings.SvgBasePath);
    }

    partial void OnDrawZtmMapChanged(bool value)
    {
        foreach (var ztmLayer in Map.Layers.OfType<ImageLayer>())
        {
            ztmLayer.Enabled = value;
        }
    }
    partial void OnSpeedVectorMinuteIntervalChanged(int value)
    {
        var layer = Map.Layers.OfType<UpdatingSpeedVectorLayer>().SingleOrDefault();
        if (layer is not null)
            layer.ObservableMinuteInterval = value;
        else
            LogExtensions.LogWarning(
                "Could not find UpdatingSpeedVectorLayer. Please ensure that the client is properly configured.", this);
    }

    partial void OnTrajectoryPointsCountChanged(int value)
    {
        var layer = Map.Layers.OfType<UpdatingTrajectoryLayer>().SingleOrDefault();
        if (layer is not null)
            layer.ObservableQueueSize = value;
        else
            LogExtensions.LogWarning(
                "Could not find UpdatingTrajectoryPointsLayer. Please ensure that the client is properly configured.",
                this);
    }

    public Map CreateMap()
    {
        var map = new Map();
        try
        {
            map.CRS = "EPSG:5514";
            // Dark grey
            map.BackColor = Color.FromString("#033052");
            map.Layers.Add(MapLayerFactory.CreateZtmDynamicLayers(ZtmDatasets.ZTM100, ZtmDatasets.ZTM25));
            map.Layers.Add(MapLayerFactory.CreateAirportElementsLayers(_layerStyleProvider).ToArray());
            if (HasClient)
            {
                map.Layers.Add(MapLayerFactory.CreatePlanesPointLayer(_dronetagClient!, _aircraftDatabase!, _svgStyleProvider));
                var trajectoryLayer = MapLayerFactory.CreateTrajectoryLayer(_dronetagClient!);
                map.Layers.Add(trajectoryLayer);
                TrajectoryPointsCount = trajectoryLayer.ObservableQueueSize;
                var speedVectorLayer = MapLayerFactory.CreateSpeedVectorLayer(_dronetagClient!);
                map.Layers.Add(speedVectorLayer);
                SpeedVectorMinuteInterval = speedVectorLayer.ObservableMinuteInterval;
            }
            else
                LogExtensions.LogError("{0} not provided. Creating map without SMAS data.", this,
                    nameof(IDronetagClient));

            // map.Home = nav => nav.CenterOnAndZoomTo(new MPoint(-539192.3d, -1184647.4d), 8);
        }
        catch (Exception e)
        {
            Greeting = e.Message;
            LogExtensions.LogError(e, "Failed to initialize map.", this);
            throw;
        }

        Map = map;
        return Map;
    }
}