using System;
using Mapsui;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Extensions;

public static class FeatureExtensions
{
    public static string GetScoutDataId(this IFeature feature)
    {
        var id = feature[ScoutData.FeatureUasIdField]?.ToString();
        if (id is not null)
            return id;

        throw new InvalidOperationException($"Could not find ScoutData ID field in feature '{feature.ToString() ?? "Unknown"}'.");
    }

    public static ScoutData? GetScoutData(this IFeature feature)
    {
        if (feature[ScoutData.FeatureScoutDataField] is ScoutData scoutData)
            return scoutData;
        
        LogExtensions.LogWarning("Feature '{0}' does not contain ScoutData field.", null, feature.ToString() ?? "UNKNOWN");
        return null;
    }
}