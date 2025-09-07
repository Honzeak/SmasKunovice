using System;
using Mapsui;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Extensions;

public static class FeatureExtensions
{
    public static string? GetFeatureId(this IFeature feature, string idField)
    {
        if (feature[idField]?.ToString() is not null)
            return feature[idField]!.ToString();

        LogExtensions.LogError("Could not find ID field in feature '{0}'.",
            null,
            feature.ToString() ?? "UNKNOWN");
        return null;
    }

    public static ScoutData? GetScoutData(this IFeature feature)
    {
        
    }
}