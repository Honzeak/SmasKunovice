using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private bool _isFeatureSelected;
    [ObservableProperty] private bool _showSelectedFeatureLabel;
    [ObservableProperty] private ObservableCollection<DataPropertyRow> _nonNullProperties = [];
    [ObservableProperty] private List<int> _trajectoryPointsViewValues = [1, 10, 100, 500];
    [ObservableProperty] private List<int> _speedVectorMinuteIntervalViewValues = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30];
    [ObservableProperty] private IReadOnlyList<string> _procedureListValues = new List<string>();
    [ObservableProperty] private string _selectedProcedureName;
    [ObservableProperty] private bool _drawCtrOrAtz = true;

    private readonly IDronetagClient? _dronetagClient;
    private readonly GeoJsonLayerStyleProvider? _layerStyleProvider;
    private readonly AircraftDatabase? _aircraftDatabase;
    private readonly SvgStyleProvider? _svgStyleProvider;
    private UpdatingPositionLayer? _positionLayer;
    private bool HasClient => _dronetagClient is not null;

    public MainViewViewModel(IDronetagClient dronetagClient, IOptions<ApplicationSettings> options) : this()
    {
        _dronetagClient = dronetagClient;
        var appSettings = options.Value;
        _layerStyleProvider = new GeoJsonLayerStyleProvider(appSettings.GeoJsonsBasePath);
        _aircraftDatabase = new AircraftDatabase(appSettings.AircraftDatabasePath);
        _svgStyleProvider = new SvgStyleProvider(appSettings.SvgBasePath);
    }

    [RelayCommand]
    private void UpdateDrawCtrOrAtz(string value)
    {
            var ctrLayers = Map.Layers.Where(layer => layer.Name.StartsWith("CTR", StringComparison.InvariantCultureIgnoreCase)).OrderBy(layer => layer.Name).ToList(); // _diff should be second
            var atzLayers = Map.Layers.Where(layer => layer.Name.StartsWith("ATZ", StringComparison.InvariantCultureIgnoreCase)).OrderBy(layer => layer.Name).ToList(); // _diff should be second
            
            if (ctrLayers.Count != 2 || atzLayers.Count != 2)
            {
                LogExtensions.LogWarning("Unexpected count of ATZ and CTR layers. Expected two for each, got {0} for CTR and {1} for ATZ", this, ctrLayers.Count, atzLayers.Count);
                return;
            }
            
            ctrLayers[0].Enabled = value == "ATZ"; // CTR outline layer is enabled when ATZ
            ctrLayers[1].Enabled = value == "CTR"; // CTR diff layer is enabled when CTR
            
            atzLayers[0].Enabled = value == "CTR"; // ATZ outline layer is enabled when CTR
            atzLayers[1].Enabled = value == "ATZ"; // ATZ diff layer is enabled when ATZ
    }

    public class DataPropertyRow
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? Value { get; set; }
    }

    private ObservableCollection<DataPropertyRow> GetNonNullProperties(AircraftRecord? record)
    {
        var displayProps = new ObservableCollection<DataPropertyRow>();
        if (record is null)
        {
            LogExtensions.LogWarning("Aircraft record is null. Nothing to display.", this);
            return displayProps;
        }

        foreach (var prop in record.GetType().GetProperties())
        {
            var value = prop.GetValue(record);

            if (value != null)
            {
                displayProps.Add(new DataPropertyRow
                {
                    // You could add logic here to split CamelCase string (e.g., "FirstName" -> "First Name")
                    PropertyName = prop.Name,
                    Value = value
                });
            }
        }

        return displayProps;
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

    partial void OnShowSelectedFeatureLabelChanged(bool value)
    {
        _positionLayer?.SetLabelVisibility(value);
    }

    partial void OnSelectedProcedureNameChanged(string value)
    {
        var procLayers = Map.Layers.Where(layer => layer.Name.StartsWith(MapLayerFactory.ProcedureLayerPrefix)).ToList();
        if (procLayers.Any())
        {
            foreach (var procLayer in procLayers)
            {
                procLayer.Enabled = procLayer.Name.Equals(MapLayerFactory.ProcedureLayerPrefix + value);
            }
        }
        else
        {
            LogExtensions.LogWarning("Could not find any procedure layers." + value, this);
        }
        Map.Refresh();
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
            map.Layers.Add(MapLayerFactory.CreateAirportElementsLayers(_layerStyleProvider, out var procedureLayerNames).ToArray());
            ProcedureListValues = procedureLayerNames;
            if (HasClient)
            {
                InitClientLayers(map);
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
        UpdateDrawCtrOrAtz("CTR"); // Need to init this to avoid drawing both CTR and ATZ
        return Map;
    }

    private void InitClientLayers(Map map)
    {
        var trajectoryLayer = MapLayerFactory.CreateTrajectoryLayer(_dronetagClient!);
        map.Layers.Add(trajectoryLayer);
        TrajectoryPointsCount = trajectoryLayer.ObservableQueueSize;

        var speedVectorLayer = MapLayerFactory.CreateSpeedVectorLayer(_dronetagClient!);
        map.Layers.Add(speedVectorLayer);
        SpeedVectorMinuteInterval = speedVectorLayer.ObservableMinuteInterval;

        _positionLayer = MapLayerFactory.CreatePlanesPointLayer(_dronetagClient!, _aircraftDatabase!, _svgStyleProvider, map);
        map.Layers.Add(_positionLayer);
        _positionLayer.SelectedFeatureChanged += (sender, feature) =>
        {
            IsFeatureSelected = feature is not null;
            ShowSelectedFeatureLabel = feature?.Styles.OfType<LabelStyle>().FirstOrDefault()?.Enabled ?? false;

            if (feature is not null)
            {
                var scoutData = feature.GetScoutData();
                if (scoutData is not null)
                {
                    var uasId = scoutData.GetUasId();
                    NonNullProperties = GetNonNullProperties(_aircraftDatabase?.GetByIcao24(uasId));
                }
                else
                {
                    NonNullProperties.Clear();
                }
            }
            else
            {
                NonNullProperties.Clear();
            }
        };
    }
}