using System;
using System.Linq;
using Avalonia.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel() : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty] private Map _map = new();
    private const string GeoJsonsBasePath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\GeoJsonElements\";

    private readonly IDronetagClient? _dronetagClient;
    private readonly GeoJsonLayerStyleProvider _layerStyleProvider = new(GeoJsonsBasePath);
    public bool HasClient => _dronetagClient is not null;

    public MainViewViewModel(IDronetagClient dronetagClient) : this()
    {
        _dronetagClient = dronetagClient;
    }

    public Map CreateMap()
    {
        var map = new Map();
        try
        {
            map.CRS = "EPSG:5514";
            map.Layers.Add(MapLayerFactory.CreateZtmDynamicLayer(ZtmDatasets.ZTM5));
            map.Layers.Add(MapLayerFactory.CreateAirportElementsLayers(_layerStyleProvider).ToArray());
            if (HasClient)
            {
                map.Layers.Add(MapLayerFactory.CreatePlanesPointLayer(_dronetagClient!));
                map.Layers.Add(MapLayerFactory.CreateTrajectoryLayer(_dronetagClient!));
                map.Layers.Add(MapLayerFactory.CreateSpeedVectorLayer(_dronetagClient!));
            }
            else
                LogExtensions.LogError("{0} not provided. Creating map without SMAS data.", this, nameof(IDronetagClient));

            map.Navigator.CenterOnAndZoomTo(new MPoint(-539192.3d, -1184647.4d), 900);
        }
        catch (Exception e)
        {
            Greeting = e.Message;
            throw;
        }

        return map;
    }
}