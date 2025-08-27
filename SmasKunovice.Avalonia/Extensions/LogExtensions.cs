using System;
using Avalonia.Logging;

namespace SmasKunovice.Avalonia.Extensions;

public static class LogExtensions
{
    public static void LogInfo(string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Information, LogArea.Control, context, message, logParams);
    }

    public static void LogDebug(string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Debug, LogArea.Control, context, message, logParams);
    }

    public static void LogError(string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, context, message, logParams);
    }
    
    public static void LogError(Exception exception, string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Error, LogArea.Control, context, message + '\n' + exception + '\n' + exception.StackTrace, logParams);
    }

    public static void LogWarning(string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Warning, LogArea.Control, context, message, logParams);
    }

    public static void LogFatal(string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Fatal, LogArea.Control, context, message, logParams);
    }
    
    public static void LogFatal(Exception exception, string message, object? context = null, params object[] logParams)
    {
        Logger.Sink?.Log(LogEventLevel.Fatal, LogArea.Control, context, message + '\n' + exception + '\n' + exception.StackTrace, logParams);
    }
}