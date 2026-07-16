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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.ConflictResolution;
using SmasKunovice.Avalonia.Models.Dronetag;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.Models.Mapsui;
using SmasKunovice.Avalonia.Views;
using MapsuiColor = Mapsui.Styles.Color;
using AvaloniaColor = Avalonia.Media.Color;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] private Map _map = new();
    [ObservableProperty] private bool _drawZtmMap = true;
    [ObservableProperty] private bool _isFeatureSelected;
    [ObservableProperty] private bool _showSelectedFeatureLabel;
    [ObservableProperty] private ObservableCollection<DataPropertyRow> _nonNullProperties = [];
    [ObservableProperty] private int _trajectoryPointsCount;
    [ObservableProperty] private List<int> _trajectoryPointsViewValues = [1, 10, 100, 500];
    [ObservableProperty] private int _speedVectorMinutes;
    [ObservableProperty] private List<int> _speedVectorMinutesViewValues = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 30];
    [ObservableProperty] private ObservableCollection<SelectProcedure> _procedureList = [];
    [ObservableProperty] private ConflictNotificationCollection _conflictNotifications = [];
    [ObservableProperty] private bool _drawCtrOrAtz = true;
    [ObservableProperty] private string _streamingStatusMessage = string.Empty;
    [ObservableProperty] private SolidColorBrush _statusBrush = new();
    [ObservableProperty] private bool _operation02C = true;
    [ObservableProperty] private bool _operation20C = true;

    private List<ILayer> _procedureLayers = [];
    private readonly GeoJsonLayerStyleProvider _layerStyleProvider;
    private readonly AircraftDatabase _aircraftDatabase;
    private readonly SvgStyleProvider _svgStyleProvider;
    private UpdatingPositionLayer? _positionLayer;
    private readonly List<ILayer> _managedLayers = [];
    private IFeature? _selectedFeature;
    private readonly DynamicScoutDataProvider _dynamicScoutDataProvider;
    private readonly IErrorDialogService _errorDialogService;
    private readonly IConflictDetectionService _conflictDetectionService;

    public MainViewViewModel(IDronetagClient dronetagClient, IErrorDialogService errorDialogService)
    {
        _layerStyleProvider = new GeoJsonLayerStyleProvider(AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "AirportElements")));
        _aircraftDatabase = new AircraftDatabase(Directory.EnumerateFiles(AssetProvider.GetFullAssetPath("Database"), "*.csv", SearchOption.TopDirectoryOnly).Single());
        _svgStyleProvider = new SvgStyleProvider(AssetProvider.GetFullAssetPath("Svg"));
        ProcedureList.CollectionChanged += OnProcedureListChanged;
        InitializeStreamingStatus(dronetagClient);
        _dynamicScoutDataProvider = new DynamicScoutDataProvider(dronetagClient);
        _errorDialogService = errorDialogService;
        _conflictDetectionService = new ConflictDetectionService(
            _dynamicScoutDataProvider, _errorDialogService, 
            ConflictDetectorFactory.CreateForDroneGrid(),
            ConflictDetectorFactory.CreateForRpaPresence(),
            ConflictDetectorFactory.CreateForRunwayApproach());
        CreateMap(new MapLayerFactory(_dynamicScoutDataProvider, _errorDialogService, _conflictDetectionService));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _dynamicScoutDataProvider.ConnectClientAsync();
            LogExtensions.LogDebug("Initialized ScoutData provider", this);
            await _conflictDetectionService.InitializeAsync();
            LogExtensions.LogDebug("Initialized ConflictDetectionService", this);
            OnOperation02CChanged(Operation02C); // Let conflict service know which runways are active
            OnOperation20CChanged(Operation20C);
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
            SetConflictChangedEvents();
            TrajectoryPointsCount = layerFactory.CreateTrajectoryLayer(_positionLayer).ObservableQueueSize;
            AddLayers(map, layerFactory.CreateTrajectoryLayer(_positionLayer), layerFactory.CreateSpeedVectorLayer(_positionLayer), _positionLayer);
        }
        catch (Exception e)
        {
            LogExtensions.LogError(e, "Failed to initialize map.", this);
            _ = _errorDialogService.ShowErrorDialogAsync("Failed to initialize map.", e);
        }

        Map = map;
        
        // Initialize UI values
        SpeedVectorMinutes = SpeedVectorMinutesViewValues[1];
        TrajectoryPointsCount = TrajectoryPointsViewValues[1];
        UpdateDrawCtrOrAtz("CTR"); 
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

        _selectedFeature[FeatureAttributes.IdOverride] = result;
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

    partial void OnSpeedVectorMinutesChanged(int value)
    {
        var layer = Map.Layers.OfType<UpdatingSpeedVectorLayer>().SingleOrDefault();
        if (layer is not null)
            layer.SpeedVectorMinutes = value;
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
    
    partial void OnOperation02CChanged(bool value)
    {
        _conflictDetectionService.SetRunwayOperation(RunwayDirection._02C, value);
    }

    partial void OnOperation20CChanged(bool value)
    {
        _conflictDetectionService.SetRunwayOperation(RunwayDirection._20C, value);
    }

    partial void OnShowSelectedFeatureLabelChanged(bool value)
    {
        _positionLayer?.SetLabelVisibility(value);
    }

    private void SetConflictChangedEvents()
    {
        _conflictDetectionService.ConflictUpdate += (sender, conflictUpdates) => HandleConflictUpdate(conflictUpdates); 
        _positionLayer?.FeatureRemoved += (sender, removeId) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var toRemove = ConflictNotifications.Where(cn => cn.UasId.Equals(removeId)).ToList();
                foreach (var notification in toRemove)
                {
                    notification.PropertyChanged -= OnConflictNotificationPropertyChanged;
                }

                ConflictNotifications.RemoveMany(toRemove);
                UpdateLabelConflictLevel(removeId);
            });
        };
    }

    private void AddConflictNotification(ConflictNotification notification)
    {
        notification.PropertyChanged += OnConflictNotificationPropertyChanged;
        ConflictNotifications.Add(notification);
        UpdateLabelConflictLevel(notification.UasId);
    }

    private void RemoveConflictNotification(PointFeature feature, ConflictType conflictType)
    {
        var notification = ConflictNotifications.FirstOrDefault(cn =>
            cn.UasId == feature.GetScoutDataId() &&
            cn.ConflictType == conflictType);

        if (notification is null)
            return;

        notification.PropertyChanged -= OnConflictNotificationPropertyChanged;
        ConflictNotifications.Remove(notification);
        UpdateLabelConflictLevel(notification.UasId);
    }

    private void OnConflictNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConflictNotification.IsMuted) &&
            e.PropertyName != nameof(ConflictNotification.ConflictLevel))
        {
            return;
        }

        if (sender is not ConflictNotification notification)
            return;

        UpdateLabelConflictLevel(notification.UasId);
    }

    private void UpdateLabelConflictLevel(string uasId)
    {
        var conflictLevel = ConflictNotifications
            .Where(notification => notification.UasId == uasId && !notification.IsMuted)
            .Select(notification => notification.ConflictLevel)
            .DefaultIfEmpty(ConflictLevel.None)
            .Max();

        _positionLayer?.SetLabelConflictLevel(uasId, conflictLevel);
    }

    private void HandleConflictUpdate(ConflictsUpdateEventArgs conflictUpdates)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (conflictUpdates.IsEnumerable)
            {
                foreach (var feature in conflictUpdates.Added)
                    AddConflictNotification(new ConflictNotification(feature, conflictUpdates.ConflictType, conflictUpdates.ConflictLevel));

                foreach (var feature in conflictUpdates.Modified)
                {
                    // TODO move this outside the collection for consistency, let's do this after splitting the view models
                    ConflictNotifications.UpdateConflictNotification(feature, conflictUpdates.ConflictType, conflictUpdates.ConflictLevel);
                    UpdateLabelConflictLevel(feature.GetScoutDataId());
                }

                foreach (var feature in conflictUpdates.Removed)
                {
                    RemoveConflictNotification(feature, conflictUpdates.ConflictType);
                }
            }
            else
            {
                // Result.Unchanged and null should not appear here
                switch (conflictUpdates.UpdateResult)
                {
                    case ConflictUpdateResult.Added:
                        AddConflictNotification(new ConflictNotification(conflictUpdates.Feature, conflictUpdates.ConflictType, conflictUpdates.ConflictLevel));
                        break;
                    case ConflictUpdateResult.Modified:
                        ConflictNotifications.UpdateConflictNotification(conflictUpdates.Feature, conflictUpdates.ConflictType, conflictUpdates.ConflictLevel);
                        UpdateLabelConflictLevel(conflictUpdates.Feature.GetScoutDataId());
                        break;
                    case ConflictUpdateResult.Removed:
                        RemoveConflictNotification(conflictUpdates.Feature, conflictUpdates.ConflictType);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        });
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
                var scoutDataId = feature.GetScoutDataId();
                NonNullProperties = GetNonNullProperties(_aircraftDatabase.GetByIcao24(scoutDataId));
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

    // TODO add dispose of newly added events
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

        _conflictDetectionService.Dispose();
        _dynamicScoutDataProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}