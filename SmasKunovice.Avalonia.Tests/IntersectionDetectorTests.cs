using System.Text.Json;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.ConflictResolution;
using SmasKunovice.Avalonia.Models.Dronetag;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class IntersectionDetectorTests : TestBase
{
    [Test]
    public void TestApproachIntersection_PointInExtentNotIntersecting_ReturnsFalse()
    {
        var message = JsonSerializer.Deserialize<ScoutData>(ScoutDataPointJson, ScoutData.SerializerOptions);
        var transform = new Wgs84ToKrovakTransformator();
        message = transform.TransformScoutDataCoords(message!);
        var approachZoneFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "ApproachProximityZone.geojson");
        var intersectionDetector = new IntersectionDetector(approachZoneFilePath);
        if (message is null)
            Assert.Fail();
        
        var result = message!.TryCreatePointFeature(out var pointFeature);
        if (!result)
            Assert.Fail();

        var intersectResult = intersectionDetector.TryGetIntersectFeature(pointFeature!, out var intersectionFeature);
        Assert.That(intersectResult, Is.False);
    }

    [Test]
    public void TestStruct() // Sanity check
    {
      const ConflictLevel none = ConflictLevel.None;
      const ConflictLevel warning = ConflictLevel.Warning;
      const ConflictLevel alarm = ConflictLevel.Alarm;

      Assert.That(none, Is.LessThan(warning));
      Assert.That(none, Is.LessThan(alarm));
      Assert.That(warning, Is.LessThan(alarm));
      Assert.That(warning, Is.GreaterThan(none));
      Assert.That(alarm, Is.GreaterThan(none));
      Assert.That(alarm, Is.GreaterThan(warning));
    }

    private const string ScoutDataPointJson = """
                                              {
                                                "sn": "D17D80CF73E53E5DF9",
                                                "mac": "00:00:49:c1:78:00",
                                                "counter": 0,
                                                "rssi": -39,
                                                "tech": "AB",
                                                "recv_id": 0,
                                                "module_id": 0,
                                                "module_type": 1090,
                                                "msg_type": 15,
                                                "noise_floor": null,
                                                "odid": {
                                                  "BasicID": [
                                                    {
                                                      "UAType": 15,
                                                      "IDType": 2,
                                                      "UASID": "48C2B7"
                                                    }
                                                  ],
                                                  "Location": {
                                                    "Status": 2,
                                                    "Longitude": 17.442319531592407,
                                                    "Latitude": 49.03283434804672,
                                                    "Direction": 205,
                                                    "SpeedHorizontal": 5.099999904632568,
                                                    "SpeedVertical": 0.0,
                                                    "AltitudeBaro": 114.0,
                                                    "AltitudeGeo": null,
                                                    "HeightType": 0,
                                                    "Height": 0.0,
                                                    "HorizAccuracy": 0,
                                                    "VertAccuracy": 0,
                                                    "BaroAccuracy": 0,
                                                    "SpeedAccuracy": 0,
                                                    "TSAccuracy": 0,
                                                    "Timestamp": "2026-04-04T10:21:37.000000"
                                                  },
                                                  "SelfID": {
                                                    "DescType": 0,
                                                    "Desc": "OKWAR17"
                                                  }
                                                }
                                              }
                                              """;
}