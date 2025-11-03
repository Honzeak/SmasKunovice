using System;
using GeoAPI.CoordinateSystems.Transformations;
using GeoAPI.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace SmasKunovice.Avalonia.Models;

public class Wgs84ToKrovakTransformator : IScoutDataCoordTransformation
{
    private readonly ICoordinateTransformation _coordinateTransformation;

    private const string KrovakWkt = """
                                     PROJCS["S-JTSK / Krovak East North",GEOGCS["S-JTSK",DATUM["System_of_the_Unified_Trigonometrical_Cadastral_Network",SPHEROID["Bessel 1841",6377397.155,299.1528128],TOWGS84[589,76,480,0,0,0,0]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],UNIT["degree",0.0174532925199433,AUTHORITY["EPSG","9122"]],AUTHORITY["EPSG","4156"]],PROJECTION["Krovak"],PARAMETER["latitude_of_center",49.5],PARAMETER["longitude_of_center",24.8333333333333],PARAMETER["azimuth",30.2881397527778],PARAMETER["pseudo_standard_parallel_1",78.5],PARAMETER["scale_factor",0.9999],PARAMETER["false_easting",0],PARAMETER["false_northing",0],UNIT["metre",1,AUTHORITY["EPSG","9001"]],AXIS["Easting",EAST],AXIS["Northing",NORTH],AUTHORITY["EPSG","5514"]]
                                     """;

    public Wgs84ToKrovakTransformator()
    {
        var cf = new CoordinateSystemFactory();
        var targetCrs = cf.CreateFromWkt(KrovakWkt);
        _coordinateTransformation =
            new CoordinateTransformationFactory().CreateFromCoordinateSystems(GeographicCoordinateSystem.WGS84,
                targetCrs);
    }

    public (double lon, double lat) TransformCoords(double longitude, double latitude)
    {
        var coord = _coordinateTransformation.MathTransform.Transform(new Coordinate(longitude, latitude));

        if (coord is null)
            throw new Exception("Failed to transfer coords.");

        return (coord.X, coord.Y);
    }

    public ScoutData TransformScoutDataCoords(ScoutData scoutData)
    {
        if (!scoutData.HasLocation)
            return scoutData;
        
        var transformedCoords = TransformCoords((double)scoutData.Odid.Location!.Longitude!, (double)scoutData.Odid.Location.Latitude!);
        scoutData.Odid.Location.SetCoords((float)transformedCoords.lon, (float)transformedCoords.lat);
        return scoutData;
    }
}

public interface IScoutDataCoordTransformation
{
    ScoutData TransformScoutDataCoords(ScoutData scoutData);
}