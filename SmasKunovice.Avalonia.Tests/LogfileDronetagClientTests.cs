using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;

namespace SmasKunovice.Avalonia.Tests;

public class LogfileDronetagClientTests : TestBase
{
    [Test]
    public void Constructor_WithValidJsonLogFile_InitializesSuccessfully()
    {
        var jsonLogFilePath = Path.Combine("TestData", nameof(LogfileDronetagClientTests), "dronetag-odid-fix.json");
        var messageReceivedEvent = new ManualResetEventSlim(false);
        ScoutData? message = null;


        var client = new LogfileDronetagClient(jsonLogFilePath, 500, new DummyTransformator());
        client.MessageReceived += (sender, args) =>
        {
            message = args.Messages.Single();
            messageReceivedEvent.Set();
        };
        client.ConnectAsync().Wait();
        var triggered = messageReceivedEvent.Wait(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(triggered, Is.True);
            Assert.That(message, Is.Not.Null);
        }

        client.Dispose();

        // Note: No cleanup needed as we're using an existing file
    }
}