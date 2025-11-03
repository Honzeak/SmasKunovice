using Mapsui;
using Mapsui.Layers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;
using SmasKunovice.Avalonia.Tests.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.Integration;

[TestFixture]
[Explicit("Integration test -  Requires a running MQTT broker and valid configuration")]
public class ScoutDataMqttClientAdapterTests
{
    [Test]
    public void TestUpdatingPositionLayerWithMqttClient()
    {
        var mockOptions = CreateClientAdapterOptions();
        using var scoutDataClient = new ScoutDataMqttClientAdapter(new Wgs84ToKrovakTransformator(), mockOptions);
        var provider = new DynamicScoutDataProvider(scoutDataClient);
        var layer = new UpdatingPositionLayer(provider);
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
        var mockOptions = CreateClientAdapterOptions();

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

    private static IOptions<ClientAdapterOptions> CreateClientAdapterOptions()
    {
        var appSettingsBasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appSettingsBasePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<App>()
            .Build();
        var clientAdapterOptions = configuration.GetSection(nameof(ClientAdapterOptions)).Get<ClientAdapterOptions>();
        Assert.That(clientAdapterOptions, Is.Not.Null);
        var mockOptions = new Mock<IOptions<ClientAdapterOptions>>();
        mockOptions.Setup(o => o.Value).Returns(clientAdapterOptions);
        return mockOptions.Object;
    }
}