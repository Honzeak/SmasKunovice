using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// A layer that updates feature locations immediately without animations.
/// Based on AnimatedPointLayer but removes all animation functionality.
/// </summary>
public abstract class UpdatingLayer<TFeature> : BaseLayer, IAsyncDataFetcher, ILayerDataSource<IProvider>,
    IModifyFeatureLayer
{
    public event EventHandler<string>? FeatureRemoved;
    private readonly IProvider _dataSource;
    protected readonly IErrorDialogService ErrorDialogService;
    private FetchInfo? _fetchInfo;
    private bool _disposed;
    private SemaphoreSlim _updateSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));

    protected UpdatingLayer(IProvider dataSource, IErrorDialogService errorDialogService)
    {
        ErrorDialogService = errorDialogService ?? throw new ArgumentNullException(nameof(errorDialogService));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        Task.Run(PeriodicUpdateAsync, _cts.Token);
        if (_dataSource is IDynamic dynamic)
            dynamic.DataChanged += OnDataSourceDataChanged;
    }

    private async Task PeriodicUpdateAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await _timer.WaitForNextTickAsync(_cts.Token);

                // Wait for semaphore outside the critical try/finally to prevent invalid releases
                await _updateSemaphore.WaitAsync(_cts.Token);
                try
                {
                    await UpdateDataAsync(false);
                }
                catch (Exception exception)
                {
                    LogExtensions.LogError(exception, "Error updating layer");
                    Dispose();
                    await _cts.CancelAsync();
                    await ErrorDialogService.ShowErrorDialogAsync("Error updating layer", exception);
                }
                finally
                {
                    _updateSemaphore.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown handling
        }
    }

    private async void OnDataSourceDataChanged(object? sender, EventArgs e)
    {
        try
        {
            await _updateSemaphore.WaitAsync(_cts.Token);
            try
            {
                await UpdateDataAsync(true);
            }
            catch (Exception exception)
            {
                LogExtensions.LogError(exception, "Error updating layer");
                Dispose();
                await _cts.CancelAsync();
                await ErrorDialogService.ShowErrorDialogAsync("Error updating layer", exception);
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected Dictionary<string, TFeature> Features { get; } = new();
    private Dictionary<string, PointFeature> PointFeatures { get; } = new();
    public override MRect? Extent => _dataSource.GetExtent();

    protected abstract void ProcessFeatures(IEnumerable<PointFeature> updateFeatures, bool reprocessing);
    protected abstract IEnumerable<IFeature> GetInterfaceFeatures();

    /// <summary>
    /// Re-generates features on the layer. Used when feature re-processing is required.
    /// </summary>
    public async Task RefreshData()
    {
        await UpdateDataAsync(false);
    }

    public void RefreshData(FetchInfo fetchInfo)
    {
        _fetchInfo = fetchInfo;
    }

    public void AbortFetch()
    {
    }

    public void ClearCache()
    {
        Features.Clear();
    }

    public IProvider? DataSource => _dataSource;

    protected async Task UpdateDataAsync(bool fetchUpdates)
    {
        if (_fetchInfo is null) return;

        if (fetchUpdates)
        {
            var updateFeatures = (await _dataSource.GetFeaturesAsync(_fetchInfo)).Cast<PointFeature>().ToList();
            foreach (var pointFeature in updateFeatures)
            {
                PointFeatures[pointFeature.GetScoutDataId()] = pointFeature;
            }

            ProcessFeatures(updateFeatures, false);
        }
        else
            ProcessFeatures(PointFeatures.Values, true);

        OnDataChanged(new DataChangedEventArgs(Name));
    }

    private IEnumerable<IFeature> GetNewFeatures(List<IFeature> features)
    {
        var newFeatures = features.Where(f =>
        {
            var featureId = f.GetScoutDataId();
            return !Features.ContainsKey(featureId);
        });
        return newFeatures;
    }

    /// <summary>
    /// Copies all fields from source feature to target feature.
    /// </summary>
    protected static void CopyFeatureFields(IFeature source, IFeature target)
    {
        foreach (var field in source.Fields)
            target[field] = source[field];
    }

    /// <summary>
    /// Finds an existing feature by comparing the ID field.
    /// </summary>
    protected TFeature? FindExistingFeature(string id)
    {
        Features.TryGetValue(id, out var existingFeature);
        return existingFeature;
    }

    public override IEnumerable<IFeature> GetFeatures(MRect? extent, double resolution)
    {
        var featureCollection = GetInterfaceFeatures();
        if (extent is null)
        {
            return new List<IFeature>();
        }

        var biggerRect = extent.Grow(
            SymbolStyle.DefaultWidth * 2 * resolution,
            SymbolStyle.DefaultHeight * 2 * resolution);

        return featureCollection.Where(f =>
        {
            var result = f.Extent?.Intersects(biggerRect) == true;
            return result;
        });
    }

    protected virtual bool RemoveFeature(string featureId)
    {
        var removed = Features.Remove(featureId);
        if (!removed)
            return false;

        FeatureRemoved?.Invoke(this, featureId);
        OnDataChanged(new DataChangedEventArgs(Name));
        return true;
    }

    // /// <summary>
    // /// Adds a new feature to the layer.
    // /// </summary>
    // /// <param name="feature">The feature to add</param>
    // public void AddFeature(PointFeature feature)
    // {
    //     _features.Add(feature);
    //     DataHasChanged();
    // }
    //
    // /// <summary>
    // /// Gets the current number of features in the layer.
    // /// </summary>
    // public int FeatureCount => _features.Count;
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _cts.Cancel();
            if (_dataSource is IDynamic dynamic)
                dynamic.DataChanged -= OnDataSourceDataChanged;

            Features.Clear();
            PointFeatures.Clear();
            FeatureRemoved = null;
            _timer.Dispose();
            _cts.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}