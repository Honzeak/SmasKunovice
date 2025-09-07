using System;
using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingSpeedVectorLayer(IProvider provider, int observableMinuteInterval = 5)
    : UpdatingLayer<GeometryFeature>(provider)
{
    private int _observableMinuteInterval = observableMinuteInterval;

    public int ObservableMinuteInterval
    {
        get => _observableMinuteInterval;
        set => _observableMinuteInterval = Math.Max(0, value);
    }

    protected override void UpdateFeaturePositions(IEnumerable<PointFeature> updateFeatures)
    {
        foreach (var pointFeature in updateFeatures)
        {
            var coordA_x = pointFeature.Point.X;
            var coordA_y = pointFeature.Point.Y;
            var featureId = pointFeature.GetFeatureId(ScoutData.FeatureUasIdField);
            var scoutData = pointFeature[ScoutData.FeatureScoutDataField];
            if (featureId is null || scoutData is null)
            {
                LogExtensions.LogError("ScoutData not found in feature. Cannot update speed vector.", this);
                if (featureId is null)
                    continue;
                
                Features.Remove(featureId);
            }
            
            var heading = scoutData.
        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        throw new NotImplementedException();
    }
}