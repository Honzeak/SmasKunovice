using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.Models.Mapsui;
using SmasKunovice.Avalonia.Views;
using MapsuiColor = Mapsui.Styles.Color;
using AvaloniaColor = Avalonia.Media.Color;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel : ViewModelBase, IDisposable
{
    public const string IdOverrideFeatureAttribute = "displayIdOverride";
    [ObservableProperty] private Map _map = new();
    [ObservableProperty] private int _trajectoryPointsCount;
    [ObservableProperty] private int _speedVectorMinuteInterval;
    [ObservableProperty] private bool _drawZtmMap = true;
    [ObservableProperty] private bool _isFeatureSelected;
    [ObservableProperty] private bool _showSelectedFeatureLabel;
    [ObservableProperty] private ObservableCollection<DataPropertyRow> _nonNullProperties = [];
    [ObservableProperty] private List<int> _trajectoryPointsViewValues = [1, 10, 100, 500];
    [ObservableProperty] private List<int> _speedVectorMinuteIntervalViewValues = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30];
    [ObservableProperty] private ObservableCollection<SelectProcedure> _procedureList = [];
    [ObservableProperty] private bool _drawCtrOrAtz = true;
    [ObservableProperty] private string _streamingStatusMessage = string.Empty;
    [ObservableProperty] private SolidColorBrush _statusBrush = new();

    private List<ILayer> _procedureLayers = [];
    private readonly GeoJsonLayerStyleProvider _layerStyleProvider;
    private readonly AircraftDatabase _aircraftDatabase;
    private readonly SvgStyleProvider _svgStyleProvider;
    private UpdatingPositionLayer? _positionLayer;
    private readonly List<ILayer> _managedLayers = [];
    private IFeature? _selectedFeature;
    private readonly DynamicScoutDataProvider _dynamicScoutDataProvider;
    private readonly IErrorDialogService _errorDialogService;

    public MainViewViewModel(IDronetagClient dronetagClient, IErrorDialogService errorDialogService)
    {
        _layerStyleProvider = new GeoJsonLayerStyleProvider(AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "AirportElements")));
        _aircraftDatabase = new AircraftDatabase(Directory.EnumerateFiles(AssetProvider.GetFullAssetPath("Database"), "*.csv", SearchOption.TopDirectoryOnly).Single());
        _svgStyleProvider = new SvgStyleProvider(AssetProvider.GetFullAssetPath("Svg"));
        ProcedureList.CollectionChanged += OnProcedureListChanged;
        InitializeStreamingStatus(dronetagClient);
        _dynamicScoutDataProvider = new DynamicScoutDataProvider(dronetagClient);
        _errorDialogService = errorDialogService;
        CreateMap(new MapLayerFactory(_dynamicScoutDataProvider, _errorDialogService));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _dynamicScoutDataProvider.ConnectClientAsync();
            LogExtensions.LogDebug("Initialized ScoutData provider", this);
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Error connecting to Dronetag client", this);
            await _errorDialogService.ShowErrorDialogAsync("Error connecting to Dronetag client", e);
        }
    }
    
    private void CreateMap(MapLayerFactory layerFactory)
    {
        var map = new Map();
        try
        {
            map.CRS = "EPSG:5514";
            // Dark grey
            map.BackColor = MapsuiColor.FromString("#033052");
            AddLayers(map, MapLayerFactory.CreateZtmDynamicLayers(ZtmDatasets.ZTM100, ZtmDatasets.ZTM25));
            AddLayers(map, layerFactory.CreateAirportElementsLayers(_layerStyleProvider, out var procedureLayerNames).ToArray());
            foreach (var procedure in CreateProceduresModelList(procedureLayerNames))
            {
                ProcedureList.Add(procedure);
            }

            _procedureLayers = map.Layers.Where(layer => layer.Name.StartsWith(MapLayerFactory.ProcedureLayerPrefix)).ToList();
            _positionLayer = layerFactory.CreatePlanesPointLayer(_aircraftDatabase, _svgStyleProvider, map);
            SetFeatureSelectedEvent();
            var trajectoryLayer = layerFactory.CreateTrajectoryLayer(_positionLayer);
            TrajectoryPointsCount = trajectoryLayer.ObservableQueueSize;
            var speedVectorLayer = layerFactory.CreateSpeedVectorLayer(_positionLayer);
            SpeedVectorMinuteInterval = speedVectorLayer.ObservableMinuteInterval;
            AddLayers(map, trajectoryLayer, speedVectorLayer, _positionLayer);
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Failed to initialize map.", this);
            _ = _errorDialogService.ShowErrorDialogAsync("Failed to initialize map.", e);
        }

        Map = map;
        UpdateDrawCtrOrAtz("CTR"); // Need to init this to avoid drawing both CTR and ATZ
    }

    private void InitializeStreamingStatus(IDronetagClient dronetagClient)
    {
        StreamingStatusMessage = dronetagClient switch
        {
            LogfileDronetagClient => "Log file replay mode",
            ScoutDataMqttClientAdapter => "Live streaming mode",
            _ => throw new ArgumentOutOfRangeException(nameof(dronetagClient))
        };
        StatusBrush = new SolidColorBrush(dronetagClient switch
        {
            LogfileDronetagClient => AvaloniaColor.Parse("#c4ba2b"),
            ScoutDataMqttClientAdapter => AvaloniaColor.Parse("#429929"),
            _ => throw new ArgumentOutOfRangeException(nameof(dronetagClient))
        });
    }

    private void OnProcedureListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (SelectProcedure newItem in e.NewItems)
            {
                newItem.PropertyChanged += OnSelectProcedureChanged;
            }
        }

        if (e.OldItems is null)
            return;

        foreach (SelectProcedure oldItem in e.OldItems)
        {
            oldItem.PropertyChanged -= OnSelectProcedureChanged;
        }
    }

    private void OnSelectProcedureChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectProcedure.IsChecked))
        {
            LogExtensions.LogWarning("Unexpected property name '{0}' in SelectProcedure.", this, e.PropertyName ?? string.Empty);
            return;
        }

        if (sender is not SelectProcedure selectedProcedure)
        {
            LogExtensions.LogError("Could not get name of changed procedure.", this);
            return;
        }

        var layer = _procedureLayers.SingleOrDefault(procLayer => procLayer.Name.Equals(MapLayerFactory.ProcedureLayerPrefix + selectedProcedure.Name));
        if (layer is null)
        {
            LogExtensions.LogWarning("Could not find matching procedure layer to {0}.", this, selectedProcedure.Name);
            return;
        }

        layer.Enabled = selectedProcedure.IsChecked;
        Map.Refresh();
    }

    [RelayCommand]
    private void UpdateDrawCtrOrAtz(string value)
    {
        var ctrLayers = Map.Layers.Where(layer => layer.Name.StartsWith("CTR", StringComparison.InvariantCultureIgnoreCase)).OrderBy(layer => layer.Name).ToList(); // _diff should be second
        var atzLayers = Map.Layers.Where(layer => layer.Name.StartsWith("ATZ", StringComparison.InvariantCultureIgnoreCase)).OrderBy(layer => layer.Name).ToList(); // _diff should be second

        if (ctrLayers.Count != 2 || atzLayers.Count != 2)
        {
            LogExtensions.LogError("Unexpected count of ATZ and CTR layers. Expected two for each, got {0} for CTR and {1} for ATZ", this, ctrLayers.Count, atzLayers.Count);
            return;
        }

        ctrLayers[0].Enabled = value == "ATZ"; // CTR outline layer is enabled when ATZ
        ctrLayers[1].Enabled = value == "CTR"; // CTR diff layer is enabled when CTR

        atzLayers[0].Enabled = value == "CTR"; // ATZ outline layer is enabled when CTR
        atzLayers[1].Enabled = value == "ATZ"; // ATZ diff layer is enabled when ATZ
        Map.Refresh();
    }

    [RelayCommand]
    private async Task OpenOverrideIdPrompt()
    {
        var dialog = new PromptWindow();
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var parent = desktop?.MainWindow;
        var result = await dialog.ShowDialog<string?>(parent!);
        if (result is null)
            return;

        if (!IsFeatureSelected || _selectedFeature is null)
            return;

        _selectedFeature[IdOverrideFeatureAttribute] = result;
        if (_positionLayer is not null)
            await _positionLayer.RefreshData();
        else
            LogExtensions.LogError("Position layer is null. Unable to refresh data.", this);
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
        foreach (var ztmLayer in Map.Layers.OfType<TileLayer>())
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

    private void SetFeatureSelectedEvent()
    {
        if (_positionLayer is null)
            return;
        
        _positionLayer.SelectedFeatureChanged += (sender, feature) =>
        {
            IsFeatureSelected = feature is not null;
            ShowSelectedFeatureLabel = feature?.Styles.OfType<LabelStyle>().FirstOrDefault()?.Enabled ?? false;

            if (feature is not null)
            {
                _selectedFeature = feature;
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

    private void AddLayers(Map map, params ILayer[] layers)
    {
        map.Layers.Add(layers);
        foreach (var layer in layers)
        {
            _managedLayers.Add(layer);
        }
    }


    private ObservableCollection<SelectProcedure> CreateProceduresModelList(IEnumerable<string> procedureLayerNames)
    {
        return new ObservableCollection<SelectProcedure>(procedureLayerNames.Select(name => new SelectProcedure(name)));
    }

    public void Dispose()
    {
        ProcedureList.CollectionChanged -= OnProcedureListChanged;

        foreach (var selectProcedure in ProcedureList)
        {
            selectProcedure.PropertyChanged -= OnSelectProcedureChanged;
        }
        
        Map.Dispose();
        foreach (var layer in _managedLayers)
        {
            layer.Dispose();
        }
        
        _dynamicScoutDataProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}