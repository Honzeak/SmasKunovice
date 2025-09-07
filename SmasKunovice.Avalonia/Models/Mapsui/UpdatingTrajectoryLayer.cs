using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingTrajectoryLayer(IProvider dataSource) : UpdatingLayer<LinkedList<PointFeature>>(dataSource)
{
    // protected override Dictionary<string, LinkedList<PointFeature>> Features { get; } = new();
    private const int QueueCapacity = 10;

    public int ObservableQueueSize
    {
        get => _observableQueueSize;
        set => _observableQueueSize = Math.Clamp(value, 0, QueueCapacity);
    }

    private int _observableQueueSize = 5;

    protected override void UpdateFeaturePositions(IEnumerable<PointFeature> updateFeatures)
    {
        foreach (var updateFeature in updateFeatures)
        {
            var featureId = updateFeature.GetFeatureId(ScoutData.FeatureUasIdField);
            if (featureId is null)
                continue;
            
            var foundLog = FindExistingFeature(featureId);
            if (foundLog is null)
            {
                var trajectoryLog = new LinkedList<PointFeature>();
                trajectoryLog.AddFirst(updateFeature);
                Features.Add(featureId, trajectoryLog);
            }
            else
            {
                if (foundLog.Count >= QueueCapacity)
                    foundLog.RemoveLast();
                foundLog.AddFirst(updateFeature);
            }
        }
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values.SelectMany(log => log.Skip(1).Take(_observableQueueSize)).ToList();
    }
}