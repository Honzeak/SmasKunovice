using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;

namespace SmasKunovice.Avalonia.Tests;

public class LogfileDronetagClientTests : TestBase
{
    [Test]
    [TestCase("mqttx-client.log")]
    [TestCase("smasKunovice-mqtt-client.log")]
    public async Task Constructor_WithValidJsonLogFile_InitializesSuccessfully(string logFileName)
    {
        const int waitTimelMs = 200;
        var jsonLogFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", nameof(LogfileDronetagClientTests), logFileName);
        var messageReceivedEvent = new ManualResetEventSlim(false);
        ScoutData? message = null;

        var options = TestHelpers.CreateClientAdapterOptions();
        options.Value.ClientSourceLogFilePath = jsonLogFilePath;
        var client = new LogfileDronetagClient(options, new DummyTransformator());
        client.MessageReceived += (sender, args) =>
        {
            message = args.Messages.Single();
            messageReceivedEvent.Set();
        };
        await client.ConnectAsync();
        var triggered = messageReceivedEvent.Wait(TimeSpan.FromMilliseconds(waitTimelMs));
        messageReceivedEvent.Reset();
        AssertMessageReceived();
        
        // wait for the second message
        triggered = messageReceivedEvent.Wait(TimeSpan.FromMilliseconds(waitTimelMs));
        AssertMessageReceived();


        client.Dispose();
        return;

        void AssertMessageReceived()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(triggered, Is.True);
                Assert.That(message, Is.Not.Null);
                TestContext.Out.WriteLine($"Received client message:\n\t{message}");
            }
        }
    }
}