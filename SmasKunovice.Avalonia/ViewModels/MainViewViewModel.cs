using System;
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
    private const string SvgBasePath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\Svg\";
    private const string GeoJsonsBasePath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\GeoJsonElements\";

    private readonly IDronetagClient? _dronetagClient;
    private readonly MapLayerFactory _mapLayerFactory = new(SvgBasePath, GeoJsonsBasePath);
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
            map.Layers.Add(_mapLayerFactory.CreateZtmDynamicLayer(ZtmDatasets.ZTM5));
            map.Layers.Add(_mapLayerFactory.CreateAirportElementsLayers());
            if (HasClient)
                map.Layers.Add(_mapLayerFactory.CreatePlanesAnimatedPointLayer(_dronetagClient!));
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