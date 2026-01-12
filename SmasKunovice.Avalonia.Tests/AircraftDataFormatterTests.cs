using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class AircraftDataFormatterTests
{
    [Test]
    public void TestGetSpeedString()
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "test" }],
                Location = new LocationData
                {
                    SpeedHorizontal = 100 // m/s
                }
            }
        };
        var result = AircraftDataFormatter.GetSpeedString(scoutData);
        Assert.That(result, Is.EqualTo(194.ToString()));
    }
    
    [Test]
    public void TestGetSpeedString_null()
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "test" }],
                Location = new LocationData()
            }
        };
        var result = AircraftDataFormatter.GetSpeedString(scoutData);
        Assert.That(result, Is.EqualTo("?"));
    }

    [Test]
    [TestCase(914.4f, "3000")] // 3000 ft
    [TestCase(1524, "FL50")]  // 5000 ft
    [TestCase(1530, "FL50")]  // 5019 ft
    [TestCase(4724.4f, "FL155")] // 15500 ft
    [TestCase(0, "?")] // 0 = null, can't have null as an input
    public void TestGetHeightString(float input, string expectedResult)
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "test" }],
                Location = new LocationData
                {
                    AltitudeGeo = input == 0 ? null : input // meters
                }
            }
        };
        
        var result = AircraftDataFormatter.GetHeightString(scoutData);
        Assert.That(result, Is.EqualTo(expectedResult));
    }
    
    [Test]
    [TestCase(0, "FL328")] // 3000 ft
    [TestCase(34.2f, "FL328↑")]  // 5000 ft
    [TestCase(62, "FL328↑")]  // 5000 ft
    [TestCase(-4.5f, "FL328↓")]  // 5019 ft
    [TestCase(-62, "FL328↓")]  // 5019 ft
    [TestCase(63, "FL328")] // 15500 ft
    [TestCase(-63, "FL328")] // 0 = null, can't have null as an input
    public void TestGetHeightString_arrow(float input, string expectedResult)
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "test" }],
                Location = new LocationData
                {
                    AltitudeGeo = 10_000,
                    SpeedVertical = input
                }
            }
        };
        
        var result = AircraftDataFormatter.GetHeightString(scoutData);
        Assert.That(result, Is.EqualTo(expectedResult));
    }
}
