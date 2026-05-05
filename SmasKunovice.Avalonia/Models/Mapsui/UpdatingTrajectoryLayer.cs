using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingTrajectoryLayer : UpdatingLayer<LinkedList<PointFeature>>
{
    // protected override Dictionary<string, LinkedList<PointFeature>> Features { get; } = new();
    private const int QueueCapacity = 500;

    public int ObservableQueueSize
    {
        get => _observableQueueSize;
        set
        {
            _observableQueueSize = Math.Clamp(value, 0, QueueCapacity);
            UpdateDataAsync(false).GetAwaiter().GetResult();
        }
    }

    private int _observableQueueSize = 1;

    public UpdatingTrajectoryLayer(IProvider dataSource, UpdatingPositionLayer? positionLayer = null) : base(dataSource)
    {
        if (positionLayer is not null)
            positionLayer.FeatureRemoved += (sender, s) => RemoveFeature(s);
    }

    protected override Task ProcessFeaturesAsync(IEnumerable<PointFeature> updateFeatures, bool reprocessing)
    {
        if (reprocessing) // We don't want to amend data when reprocessing, just change number of returned points on the interface
            return Task.CompletedTask;
        
        foreach (var updateFeature in updateFeatures)
        {
            var featureId = updateFeature.GetScoutDataId();
            var foundLog = FindExistingFeature(featureId);
            if (foundLog is null)
            {
                var trajectoryLog = new LinkedList<PointFeature>();
                trajectoryLog.AddFirst(updateFeature);
                Features.Add(featureId, trajectoryLog);
            }
            else
            {
                // In return method, we skip the first point, so we need to track capacity + 1 points
                if (foundLog.Count >= QueueCapacity + 1) 
                    foundLog.RemoveLast();
                foundLog.AddFirst(updateFeature);
            }
        }
        
        return Task.CompletedTask;
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values.SelectMany(log => log.Skip(1).Take(_observableQueueSize)).ToList();
    }
}