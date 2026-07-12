using System;
using Mapsui;
using SmasKunovice.Avalonia.Models.Dronetag;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Extensions;

public static class FeatureExtensions
{
    extension(IFeature feature)
    {
        public string GetScoutDataId()
        {
            var id = feature[FeatureAttributes.UasId]?.ToString();
            return id ?? throw new InvalidOperationException($"Could not find ScoutData ID field in feature '{feature.ToString() ?? "Unknown"}'.");
        }

        public ScoutData GetScoutData()
        {
            if (feature[FeatureAttributes.ScoutData] is ScoutData scoutData)
                return scoutData;

            throw new InvalidOperationException($"Feature '{feature.ToString() ?? "UNKNOWN"}' does not contain ScoutData field.");
        }

        public string GetAircraftDisplayId()
        {
            string displayId;
            if (feature[FeatureAttributes.IdOverride] is string overrideId)
            {
                displayId = overrideId;
            }
            else if (feature[FeatureAttributes.AircraftRegistration] is string aircraftRegistration)
            {
                displayId = aircraftRegistration;
            }
            else
            {
                displayId = feature.GetScoutDataId();
            }

            return displayId;
        }
    }
}