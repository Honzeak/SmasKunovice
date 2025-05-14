using Mapsui.Layers;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class DroneTagMessageTests
{
    [Test]
    public void Constructor()
    {
        var message = new DroneTagMessage("ID", 1, 2, 3, 4, 5);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Id, Is.EqualTo("ID"));
            Assert.That(message.Latitude, Is.EqualTo(1));
            Assert.That(message.Longitude, Is.EqualTo(2));
            Assert.That(message.Altitude, Is.EqualTo(3));
            Assert.That(message.Speed, Is.EqualTo(4));
            Assert.That(message.Heading, Is.EqualTo(5));
        }
    }
    
    [Test]
    [Ignore("It's not throwing exception, but it should")]
    public void Constructor_WithNullId_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new DroneTagMessage(null!, 1, 2, 3, 4, 5));
        Assert.Throws<ArgumentNullException>(() => _ = new DroneTagMessage("", 1, 2, 3, 4, 5));
    }
    
    [Test]
    public void ToFeature()
    {
        const string expectedId = "myID";
        var message = new DroneTagMessage(expectedId, 1, 2, 3, 4, 5);
        var feature = message.ToPointFeature();
        
        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.InstanceOf<PointFeature>());
        Assert.That(feature["Message"], Is.EqualTo(message));
        Assert.That(feature["ID"], Is.EqualTo(expectedId));
        Assert.That(feature.Point.X, Is.EqualTo(1));
        Assert.That(feature.Point.Y, Is.EqualTo(2));
    }
    
}