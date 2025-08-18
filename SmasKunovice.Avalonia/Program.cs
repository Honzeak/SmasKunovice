using Avalonia;
using System;
using Avalonia.Logging;
using SmasKunovice.Avalonia.Views;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace SmasKunovice.Avalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ =>
            {
#if DEBUG
                var level = LogEventLevel.Debug;
#endif
                Logger.Sink = new SerilogSink(level);
                
            });
}