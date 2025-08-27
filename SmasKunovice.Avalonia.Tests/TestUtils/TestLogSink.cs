using Avalonia.Logging;

namespace SmasKunovice.Avalonia.Tests.TestUtils;

public class TestLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => true;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        TestContext.Out.WriteLine($"[{level}] {area}: {messageTemplate}");
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        TestContext.Out.WriteLine($"[{level}] {area}: {messageTemplate}", propertyValues);
    }
}