using System.Text.Json;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Tests.TestUtils;

namespace SmasKunovice.Avalonia.Tests;

public class ScoutDataTests : TestBase
{
    private Dictionary<string, string> _testFiles;

    [SetUp]
    public void Setup()
    {
        _testFiles = FileUtilities.GetTestFiles(nameof(ScoutDataTests));
        // _testFiles = Directory
        //     .EnumerateFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData"), $"{nameof(ScoutDataTests)}*.json")
        //     .ToDictionary(path => Path.GetFileNameWithoutExtension(path).Replace(nameof(ScoutDataTests), string.Empty),
        //         path => path);
    }

    [Test]
    public void DeserializeTest()
    {
        var jsonStream = File.OpenRead(_testFiles["_single_1"]);
        var obj = JsonSerializer.Deserialize<ScoutData>(jsonStream, ScoutData.SerializerOptions);
        Assert.That(obj, Is.Not.Null);
    }

    [Test]
    public void InitMinimalLocationMessageTest()
    {
        const string id = "Letadylko1";
        const float latitude = 49.208333f;
        const float longitude = 16.566667f;
        const int heading = 270;
        const int speed = 10;
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = id }],
                Location = new LocationData
                {
                    Latitude = latitude, Longitude = longitude, Direction = heading,
                    SpeedHorizontal = (float)speed
                }
            }
        };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(scoutData.Odid.BasicId[0], Is.Not.Null);
            Assert.That(scoutData.Odid.Location, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scoutData.Odid.BasicId[0].UasId, Is.EqualTo(id));
            Assert.That(scoutData.Odid.Location.Latitude, Is.EqualTo(latitude));
            Assert.That(scoutData.Odid.Location.Longitude, Is.EqualTo(longitude));
            Assert.That(scoutData.Odid.Location.Direction, Is.EqualTo(heading));
            Assert.That(scoutData.Odid.Location.SpeedHorizontal, Is.EqualTo(speed));
        }
    }

    [Test]
    public void ToPointFeatureTest()
    {
        const string id = "Letadylko1";
        const float latitude = 49.208333f;
        const float longitude = 16.566667f;
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = id }],
                Location = new LocationData
                {
                    Latitude = latitude, Longitude = longitude
                }
            }
        };

        Assert.That(scoutData.TryCreatePointFeature(out var pointFeature), Is.True);
        Assert.That(pointFeature, Is.Not.Null);
        Assert.That(pointFeature.Point.Y, Is.EqualTo(latitude));
        Assert.That(pointFeature.Point.X, Is.EqualTo(longitude));
        Assert.That(pointFeature[ScoutData.FeatureUasIdField], Is.EqualTo(id));
        Assert.That(pointFeature[ScoutData.FeatureScoutDataField], Is.EqualTo(scoutData));
        
    }
    
    [TestCase("2026-06-18T12:34:56.123456Z", true)]
    [TestCase("2026-06-18T12:34:56.123456", false)]
    public void GetTimestamp_WithSupportedTimestampFormats_ReturnsParsedDateTime(string timestamp, bool isUtc)
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "Letadylko1" }],
                Location = new LocationData
                {
                    Timestamp = timestamp
                }
            }
        };

        var result = scoutData.Odid.Location.GetTimestamp();

        var expectedDateTime = new DateTime(2026, 6, 18, 12, 34, 56, 123).AddTicks(4560);
        Assert.That(result, Is.Not.Null);
        if (isUtc)
            result = result.Value.ToUniversalTime();
        Assert.That(result, Is.EqualTo(expectedDateTime));
    }

    [Test]
    public void GetTimestamp_WhenLocationIsNull_ReturnsNull()
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "Letadylko1" }],
                Location = null
            }
        };

        var result = scoutData.Odid.Location?.GetTimestamp();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetTimestamp_WhenTimestampIsNull_ReturnsNull()
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "Letadylko1" }],
                Location = new LocationData
                {
                    Timestamp = null
                }
            }
        };

        var result = scoutData.Odid.Location.GetTimestamp();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetTimestamp_WhenTimestampHasInvalidFormat_ReturnsNull()
    {
        var scoutData = new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = "Letadylko1" }],
                Location = new LocationData
                {
                    Timestamp = "invalid timestamp"
                }
            }
        };

        var result = scoutData.Odid.Location.GetTimestamp();

        Assert.That(result, Is.Null);
    }
}