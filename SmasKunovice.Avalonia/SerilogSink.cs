using System;
using System.IO;
using Avalonia.Logging;
using Serilog;

namespace SmasKunovice.Avalonia;

public class SerilogSink : ILogSink
{
    private const int RetainedFileCountLimit = 15;
    private readonly Serilog.Core.Logger _logger;

    public SerilogSink(Serilog.Events.LogEventLevel minLevel = Serilog.Events.LogEventLevel.Information,
        string? logFilePath = null)
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmasKunovice", "Logs");

        Directory.CreateDirectory(logsDirectory);

        logFilePath ??= Path.Combine(logsDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");

        // Configure and create Serilog logger
        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.Console()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Area}] {Message}{NewLine}{Exception}",
                retainedFileCountLimit: RetainedFileCountLimit)
            .CreateLogger();

        // Log the initialization of the logger
        _logger.Information("Logging initialized. Logs will be written to: {LogFilePath}", logFilePath);
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return true;
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        var serilogLevel = MapToSeriLogLevel(level);
        var formattedSource = GetFormattedSource(source);
        var template = $"[{area}]{formattedSource} {messageTemplate}";
        _logger.Write(serilogLevel, template);
    }

    private static string GetFormattedSource(object? source)
    {
        var sourceToString = source?.ToString();
        if (sourceToString is null)
            return "";

        var lastDotIndex = sourceToString.LastIndexOf('.');
        return $"[{sourceToString[(lastDotIndex == -1 ? 0 : lastDotIndex + 1)..]}]";
    }

    private static Serilog.Events.LogEventLevel MapToSeriLogLevel(LogEventLevel avaloniaLevel)
    {
        return avaloniaLevel switch
        {
            LogEventLevel.Verbose => Serilog.Events.LogEventLevel.Verbose,
            LogEventLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogEventLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogEventLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogEventLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogEventLevel.Fatal => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
        params object?[] propertyValues)
    {
        var serilogLevel = MapToSeriLogLevel(level);
        var template = $"[{area}]{GetFormattedSource(source)} {messageTemplate}";
        _logger.Write(serilogLevel, template, propertyValues);
    }
}