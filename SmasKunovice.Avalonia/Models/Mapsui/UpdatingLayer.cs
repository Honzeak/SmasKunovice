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
using NetTopologySuite.Geometries;

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// A layer that updates feature locations immediately without animations.
/// Based on AnimatedPointLayer but removes all animation functionality.
/// </summary>
public abstract class UpdatingLayer<TFeature> : BaseLayer, IAsyncDataFetcher, ILayerDataSource<IProvider>,
    IModifyFeatureLayer
{
    private readonly IProvider _dataSource;
    private FetchInfo? _fetchInfo;

    [SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates")]
    protected UpdatingLayer(IProvider dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        if (_dataSource is IDynamic dynamic)
            dynamic.DataChanged += (s, e) =>
            {
                Catch.Exceptions(async () =>
                {
                    await UpdateDataAsync();
                    DataHasChanged();
                });
            };
    }

    protected Dictionary<string, TFeature> Features { get; } = new();
    public override MRect? Extent => _dataSource.GetExtent();
    
    protected abstract void UpdateFeaturePositions(IEnumerable<PointFeature> updateFeatures);
    protected abstract IEnumerable<IFeature> GetInterfaceFeatures();

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

    private async Task UpdateDataAsync()
    {
        if (_fetchInfo is null) return;

        var features = await _dataSource.GetFeaturesAsync(_fetchInfo);
        UpdateFeaturePositions(features.Cast<PointFeature>());
        ApplyFeaturesLabelStyle();
        OnDataChanged(new DataChangedEventArgs(Name));
    }

    protected abstract void ApplyFeaturesLabelStyle();


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

    // public bool RemoveFeature(object featureId)
    // {
    //     var featureToRemove = _features.FirstOrDefault(f => f[IdField]?.Equals(featureId) ?? false);
    //     if (featureToRemove == null)
    //         return false;
    //     _features.Remove(featureToRemove);
    //     DataHasChanged();
    //     return true;
    // }
    //
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