using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Extensions;
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
    private FetchInfo? _fetchInfo;

    protected UpdatingLayer(IProvider dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        if (_dataSource is IDynamic dynamic)
            dynamic.DataChanged += (s, e) =>
            {
                Catch.Exceptions(async () =>
                {
                    await UpdateDataAsync(true);
                });
            };
    }

    protected Dictionary<string, TFeature> Features { get; } = new();
    private Dictionary<string, PointFeature> PointFeatures { get; } = new();
    public override MRect? Extent => _dataSource.GetExtent();
    
    protected abstract Task ProcessFeaturesAsync(IEnumerable<PointFeature> updateFeatures, bool reprocessing);
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
            foreach (var kvp in updateFeatures.ToDictionary(pf => pf.GetScoutDataId() ?? "UNKNOWN" ).Where(kvp => !kvp.Key.Equals("UNKNOWN")))
            {
                PointFeatures[kvp.Key] = kvp.Value;
            }

            await ProcessFeaturesAsync(updateFeatures, false);
        }
        else
            await ProcessFeaturesAsync(PointFeatures.Values, true);
        
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

    protected bool RemoveFeature(string featureId)
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
}