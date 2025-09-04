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

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// A layer that updates feature locations immediately without animations.
/// Based on AnimatedPointLayer but removes all animation functionality.
/// </summary>
public abstract class UpdatingLayer<TFeatures, TFeature> : BaseLayer, IAsyncDataFetcher, ILayerDataSource<IProvider>,
    IModifyFeatureLayer where TFeatures : Dictionary<string, TFeature>
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

    protected abstract TFeatures Features { get; }

    public override MRect? Extent => _dataSource.GetExtent();

    public void RefreshData(FetchInfo fetchInfo)
    {
        _fetchInfo = fetchInfo;
    }

    public void AbortFetch()
    {
    }

    public abstract void ClearCache();

    public IProvider? DataSource => _dataSource;

    /// <summary>
    /// The field name used to identify features when updating their positions.
    /// Default is "ID".
    /// </summary>
    private async Task UpdateDataAsync()
    {
        if (_fetchInfo is null) return;

        var features = await _dataSource.GetFeaturesAsync(_fetchInfo);
        UpdateFeaturePositions(features.Cast<PointFeature>());
        OnDataChanged(new DataChangedEventArgs(Name));
    }

    /// <summary>
    /// Updates feature positions immediately without animation.
    /// </summary>
    /// <param name="updatedFeatures">The features with new positions</param>
    protected abstract void UpdateFeaturePositions(IEnumerable<PointFeature> updatedFeatures);

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
    protected static TFeature? FindExistingFeature(TFeatures features, string id)
    {
        features.TryGetValue(id, out var existingFeature);
        return existingFeature;
    }

    public override IEnumerable<IFeature> GetFeatures(MRect extent, double resolution)
    {
        return ConvertToFeaturesOnInterface(Features);
    }

    protected abstract IEnumerable<IFeature> ConvertToFeaturesOnInterface(TFeatures featuresImpl);

    /// <summary>
    /// This layer doesn't use animations, so always returns false.
    /// </summary>
    public override bool UpdateAnimations()
    {
        return false; // No animations to update
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