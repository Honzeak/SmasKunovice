using Mapsui;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;
using SmasKunovice.Avalonia.Tests.Mapsui;
using SmasKunovice.Avalonia.Tests.TestUtils;

namespace SmasKunovice.Avalonia.Tests.Integration;

[TestFixture]
[Explicit("Integration test -  Requires a running MQTT broker and valid configuration")]
public class ScoutDataMqttClientAdapterTests
{
    [Test]
    public void TestUpdatingPositionLayerWithMqttClient()
    {
        var mockOptions = TestHelpers.CreateClientAdapterOptions();
        using var scoutDataClient = new ScoutDataMqttClientAdapter(new Wgs84ToKrovakTransformator(), mockOptions);
        var provider = new DynamicScoutDataProvider(scoutDataClient);
        var layer = new UpdatingPositionLayer(provider, new DummyAircraftDatabase(), new DummyAircraftSymbolProvider(), new Map());
        layer.RefreshData(new FetchInfo(new MSection(TestDroneTagClient.GetExtent(), 1)));
        var messageReceivedEvent = new ManualResetEventSlim(false);
        layer.DataChanged += (sender, args) =>
        {
            messageReceivedEvent.Set();
            var features = layer.GetFeatures(TestDroneTagClient.GetExtent(), 1);
        };
        var eventFired = messageReceivedEvent.Wait(TimeSpan.FromSeconds(11));
        Assert.That(eventFired, Is.True);
    }
    
    [Test]
    public void ConnectAsync_WithValidOptions_ShouldConnect()
    {
        var mockOptions = TestHelpers.CreateClientAdapterOptions();

        using var dronetagClient = new ScoutDataMqttClientAdapter(new DummyTransformator(), mockOptions);
        dronetagClient.ConnectAsync().GetAwaiter().GetResult();
        var messageReceivedEvent = new ManualResetEventSlim(false);
        dronetagClient.MessageReceived += (sender, e) =>
        {
            messageReceivedEvent.Set();
            Console.WriteLine(e);
        };
        var eventFired = messageReceivedEvent.Wait(TimeSpan.FromSeconds(11));
        Assert.That(eventFired, Is.True);
    }
}