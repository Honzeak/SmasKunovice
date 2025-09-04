using Mapsui;

namespace SmasKunovice.Avalonia.Extensions;

public static class FeatureExtensions
{
    public const string IdField = "ID";

    public static string? GetFeatureId(this IFeature feature)
    {
        if (feature[IdField]?.ToString() is not null)
            return feature[IdField]!.ToString();

        LogExtensions.LogError("Could not find ID field in new incoming feature '{0}'. Unable to register position.",
            null,
            feature.ToString() ?? "UNKNOWN");
        return null;
    }
}