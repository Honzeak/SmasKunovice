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

// #pragma warning disable IDISP001 // Dispose created

namespace SmasKunovice.Avalonia.Models.Mapsui;

/// <summary>
/// A layer that updates feature locations immediately without animations.
/// Based on AnimatedPointLayer but removes all animation functionality.
/// </summary>
public class UpdatingPointLayer : BaseLayer, IAsyncDataFetcher, ILayerDataSource<IProvider>, IModifyFeatureLayer
{
    private readonly IProvider _dataSource;
    private FetchInfo? _fetchInfo;
    private readonly List<PointFeature> _features = [];

    [SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates")]
    public UpdatingPointLayer(IProvider dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentException(nameof(dataSource));
        if (_dataSource is IDynamic dynamic)
            dynamic.DataChanged += (s, e) =>
            {
                Catch.Exceptions(async () =>
                {
                    await UpdateDataAsync();
                    DataHasChanged();
                });
            };

        // Field used to identify features for updates
        IdField = "ID";
    }

    /// <summary>
    /// The field name used to identify features when updating their positions.
    /// Default is "ID".
    /// </summary>
    private string IdField { get; set; }

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
    private void UpdateFeaturePositions(IEnumerable<PointFeature> updatedFeatures)
    {
        foreach (var updatedFeature in updatedFeatures)
        {
            var existingFeature = FindExistingFeature(_features, updatedFeature, IdField);
            if (existingFeature is null)
            {
                // Create new feature if it doesn't exist
                var newFeature = new PointFeature(updatedFeature.Point.X, updatedFeature.Point.Y);
                CopyFeatureFields(updatedFeature, newFeature);
                _features.Add(newFeature);
            }
            else
            {
                // Update existing feature position immediately
                existingFeature.Point.X = updatedFeature.Point.X;
                existingFeature.Point.Y = updatedFeature.Point.Y;
                CopyFeatureFields(updatedFeature, existingFeature);
            }
        }
    }

    /// <summary>
    /// Copies all fields from source feature to target feature.
    /// </summary>
    private static void CopyFeatureFields(IFeature source, IFeature target)
    {
        foreach (var field in source.Fields)
            target[field] = source[field];
    }

    /// <summary>
    /// Finds an existing feature by comparing the ID field.
    /// </summary>
    private static PointFeature? FindExistingFeature(IEnumerable<PointFeature>? features, IFeature feature, string idField)
    {
        // There is no guarantee the idField is set since the features are added by the user.
        return features?.FirstOrDefault(f => f[idField]?.Equals(feature[idField]) ?? false);
    }

    public override MRect? Extent => _dataSource.GetExtent();

    public override IEnumerable<IFeature> GetFeatures(MRect extent, double resolution)
    {
        return _features;
    }

    public void RefreshData(FetchInfo fetchInfo)
    {
        _fetchInfo = fetchInfo;
    }

    /// <summary>
    /// This layer doesn't use animations, so always returns false.
    /// </summary>
    public override bool UpdateAnimations()
    {
        return false; // No animations to update
    }

    public void AbortFetch()
    {
    }

    public void ClearCache()
    {
        _features.Clear();
    }

    public IProvider? DataSource => _dataSource;

    /// <summary>
    /// Removes a feature from the layer by its ID.
    /// </summary>
    /// <param name="featureId">The ID of the feature to remove</param>
    /// <returns>True if the feature was found and removed, false otherwise</returns>
    public bool RemoveFeature(object featureId)
    {
        var featureToRemove = _features.FirstOrDefault(f => f[IdField]?.Equals(featureId) ?? false);
        if (featureToRemove == null)
            return false;
        _features.Remove(featureToRemove);
        DataHasChanged();
        return true;
    }

    /// <summary>
    /// Adds a new feature to the layer.
    /// </summary>
    /// <param name="feature">The feature to add</param>
    public void AddFeature(PointFeature feature)
    {
        _features.Add(feature);
        DataHasChanged();
    }

    /// <summary>
    /// Gets the current number of features in the layer.
    /// </summary>
    public int FeatureCount => _features.Count;
}