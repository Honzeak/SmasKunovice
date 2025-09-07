using System;
using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers;
using NetTopologySuite.Geometries;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class UpdatingSpeedVectorLayer(IProvider provider, int observableMinuteInterval = 5) : UpdatingLayer<GeometryFeature>(provider)
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
    }

    private GeometryFeature CreateSpeedVectorFeature(double coordAx, double coordAy, int headingDegrees, float speedMps)
    {
        var (coordBx, coordBy) = CalculateVectorCoords(coordAx, coordAy, headingDegrees, speedMps);
        var geometryFactory = new GeometryFactory();
        var cordA = new Coordinate(coordAx, coordAy);
        var cordB = new Coordinate(coordBx, coordBy);
        var lineString = geometryFactory.CreateLineString([cordA, cordB]);
        return new GeometryFeature(lineString);
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