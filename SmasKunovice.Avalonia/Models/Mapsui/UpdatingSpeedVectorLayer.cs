using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingSpeedVectorLayer : UpdatingLayer<GeometryFeature>
{
    private int _observableMinuteInterval;
    private readonly VectorStyle _solidVectorStyle = new() 
    { 
        Line = new Pen
        {
            Color = Color.FromString("#c3fc05"),
            Width = 2
        }
    };

    public UpdatingSpeedVectorLayer(IProvider provider, UpdatingPositionLayer? positionLayer = null, int observableMinuteInterval = 5) : base(provider)
    {
        _observableMinuteInterval = observableMinuteInterval;
        if (positionLayer is not null)
            positionLayer.FeatureRemoved += (sender, s) => RemoveFeature(s);
    }

    public int ObservableMinuteInterval
    {
        get => _observableMinuteInterval;
        set
        {
            _observableMinuteInterval = Math.Max(0, value);
            UpdateDataAsync(false).GetAwaiter().GetResult();
        }
    }

    protected override Task ProcessFeaturesAsync(IEnumerable<PointFeature> updateFeatures, bool reprocessing)
    {
        foreach (var pointFeature in updateFeatures)
        {
            var coordAx = pointFeature.Point.X;
            var coordAy = pointFeature.Point.Y;
            var featureId = pointFeature.GetFeatureId(ScoutData.FeatureUasIdField);
            var scoutData = pointFeature.GetScoutData();
            var heading = scoutData?.Odid?.Location?.Direction;
            var speed = scoutData?.Odid?.Location?.SpeedHorizontal;
            if (featureId is null)
                continue;

            if (heading is null || speed is null)
            {
                LogExtensions.LogError("Horizontal speed and heading not found in feature. Cannot create speed vector.", this);
                Features.Remove(featureId);
                continue;
            }
            
            Features[featureId] = CreateSpeedVectorFeature(coordAx, coordAy, heading.Value, speed.Value);
        }
        return Task.CompletedTask;
    }

    private GeometryFeature CreateSpeedVectorFeature(double coordAx, double coordAy, int headingDegrees, float speedMps)
    {
        var (coordBx, coordBy) = CalculateVectorCoords(coordAx, coordAy, headingDegrees, speedMps);
        var geometryFactory = new GeometryFactory();
        
        // Create a MultiLineString with segments representing each minute interval
        var lineStrings = CreateDashedLineSegments(coordAx, coordAy, coordBx, coordBy, geometryFactory);
        var multiLineString = geometryFactory.CreateMultiLineString(lineStrings);
        
        var feature = new GeometryFeature(multiLineString);
        feature.Styles.Add(_solidVectorStyle);
        return feature;
    }

    private LineString[] CreateDashedLineSegments(double startX, double startY, double endX, double endY, GeometryFactory geometryFactory)
    {
        var totalDistance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        
        // Calculate segment and gap lengths
        // Each interval gets a segment and a gap, except the last one which is just a segment
        var segmentLength = totalDistance / _observableMinuteInterval;
        var gapRatio = 0.15; // 15% gap, 85% segment
        var actualSegmentLength = segmentLength * (1 - gapRatio);
        
        // Direction vector
        var dirX = (endX - startX) / totalDistance;
        var dirY = (endY - startY) / totalDistance;
        
        var lineStrings = new List<LineString>();
        var currentDistance = 0.0;
        
        for (int i = 0; i < _observableMinuteInterval; i++)
        {
            // Start of this segment
            var segmentStartX = startX + dirX * currentDistance;
            var segmentStartY = startY + dirY * currentDistance;
            
            // End of this segment
            var segmentEndX = startX + dirX * (currentDistance + actualSegmentLength);
            var segmentEndY = startY + dirY * (currentDistance + actualSegmentLength);
            
            var cordA = new Coordinate(segmentStartX, segmentStartY);
            var cordB = new Coordinate(segmentEndX, segmentEndY);
            var lineString = geometryFactory.CreateLineString([cordA, cordB]);
            lineStrings.Add(lineString);
            
            // Move to next segment (segment + gap)
            currentDistance += segmentLength;
        }
        
        return lineStrings.ToArray();
    }

    private (double coordBx, double coordBy) CalculateVectorCoords(double coordAx, double coordAy, int headingDegrees, float speedMps)
    {
        var headingRadians = headingDegrees * Math.PI / 180.0;
        // Meters
        var distance = speedMps * _observableMinuteInterval * 60d;
        // EPSG:5514 is in meters, so safe to transform using pythagorean theorem
        // 0 deg. heading should be North, therefore sin and cos are swapped.
        var deltaX = distance * Math.Sin(headingRadians);
        var deltaY = distance * Math.Cos(headingRadians);
        var coordBx = coordAx + deltaX;
        var coordBy = coordAy + deltaY;

        return (coordBx, coordBy);
    }

    protected override IEnumerable<IFeature> GetInterfaceFeatures()
    {
        return Features.Values;
    }
}