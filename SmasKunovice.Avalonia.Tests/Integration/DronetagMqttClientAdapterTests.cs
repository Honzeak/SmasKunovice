using Castle.Core.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using MQTTnet;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests.Integration;

public class DronetagMqttClientAdapterTests
{
    [Test]
    [Explicit("Integration test -  bRequires a running MQTT broker and valid configuration")]
    public void ConnectAsync_WithValidOptions_ShouldConnect()
    {
        // Arrange
        var testBaseDirectory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (testBaseDirectory!.Name != nameof(SmasKunovice))
            testBaseDirectory = testBaseDirectory.Parent;

        var appSettingsBasePath = testBaseDirectory
            .GetDirectories("SmasKunovice.Avalonia", SearchOption.TopDirectoryOnly)
            .Single().FullName;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appSettingsBasePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<App>()
            .Build();
        var clientAdapterOptions = configuration.GetSection(nameof(ClientAdapterOptions)).Get<ClientAdapterOptions>();
        Assert.That(clientAdapterOptions, Is.Not.Null);

        var mockOptions = new Mock<IOptions<ClientAdapterOptions>>();
        mockOptions.Setup(o => o.Value).Returns(clientAdapterOptions);
        using var dronetagClient = new DronetagMqttClientAdapter(mockOptions.Object);
        dronetagClient.ConnectAsync().Wait();
        var messageReceivedEvent = new ManualResetEventSlim(false);
        dronetagClient.HeartbeatReceived += (sender, e) =>
        {
            messageReceivedEvent.Set();
            Console.WriteLine(e);
        };
        var eventFired = messageReceivedEvent.Wait(TimeSpan.FromSeconds(11));
        Assert.That(eventFired, Is.True);
    }
}