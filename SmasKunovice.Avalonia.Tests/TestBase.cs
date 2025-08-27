using Avalonia.Logging;
using SmasKunovice.Avalonia.Tests.TestUtils;

namespace SmasKunovice.Avalonia.Tests;

public abstract class TestBase
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Set up logger for all tests
        Logger.Sink = new TestLogSink();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Sink = null;
    }
}