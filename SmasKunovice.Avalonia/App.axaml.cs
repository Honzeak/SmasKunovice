using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Mapsui.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Config;
using SmasKunovice.Avalonia.Models.Dronetag;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.ViewModels;
using SmasKunovice.Avalonia.Views;

namespace SmasKunovice.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private readonly IErrorDialogService _errorDialogService = new ErrorDialogService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        SetupExceptionHandling();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
// #if DEBUG
            .AddJsonFile("appsettings.User.json", optional: false, reloadOnChange: true)
// #endif
            .Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var services = ConfigureServices(configuration);

// #if !DEBUG
//             if (!await HandleUserAuthentication(desktop, services)) return;
// #endif

            _serviceProvider = services.BuildServiceProvider();
            try
            {
                var mainViewViewModel = _serviceProvider.GetRequiredService<MainViewViewModel>();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(mainViewViewModel),
                };
                desktop.MainWindow.Show();
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
                _ = mainViewViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogExtensions.LogError(ex, "Failed to initialize application.", this);
                Dispatcher.UIThread.Post(async () =>
                {
                    await _errorDialogService.ShowErrorDialogAsync("Failed to initialize application.", ex);
                    desktop.Shutdown(1);
                });
            }

            desktop.Exit += (s, e) =>
            {
                LogExtensions.LogInfo("Application exit started.", this);
                _serviceProvider?.Dispose();
                LogExtensions.LogInfo("Service provider disposed.", this);
                _serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<bool> HandleUserAuthentication(IClassicDesktopStyleApplicationLifetime desktop, ServiceCollection services)
    {
        var loginWindow = new LoginWindow();
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        var tcs = new TaskCompletionSource<bool>();
        loginWindow.Closed += (s, e) => tcs.SetResult(true);
        await tcs.Task;
        var loginVm = (LoginViewModel)loginWindow.DataContext!;
        if (!loginVm.Success)
        {
            desktop.Shutdown();
            return false;
        }

        services.PostConfigure<ClientAdapterOptions>(options =>
        {
            options.Username = loginVm.Username;
            options.Password = loginVm.Password;
        });
        return true;
    }

    private void SetupExceptionHandling()
    {
        Logger.LogDelegate += (level, message, exception) =>
        {
            switch (level)
            {
                case LogLevel.Error:
                    if (exception is not null)
                        LogExtensions.LogError(exception, "Mapsui processing error.", this);
                    else
                        LogExtensions.LogError($"Mapsui processing error: {message}", this);
                    // This is safe to run out-of-band; our service marshals back to UI Thread internally
                    Dispatcher.UIThread.Post(() => _errorDialogService.ShowErrorDialogAsync(
                        "Mapsui Processing Error",
                        exception
                    ));
                    break;
                case LogLevel.Warning:
                    LogExtensions.LogWarning($"Mapsui warning: {message}", this);
                    break;
                case LogLevel.Information:
                    LogExtensions.LogInfo($"Mapsui information: {message}", this);
                    break;
                case LogLevel.Debug:
                    LogExtensions.LogDebug($"Mapsui debug: {message}", this);
                    break;
                case LogLevel.Trace:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        };

        // 2. Trap Avalonia UI Dispatcher UI Thread Crashes
        Dispatcher.UIThread.UnhandledException += (sender, args) =>
        {
            // Instructs Avalonia to recover from the crash state and continue running
            args.Handled = true;
            _ = _errorDialogService.ShowErrorDialogAsync("UI Render Thread Exception", args.Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Dispatcher.UIThread.Post(() => _errorDialogService.ShowErrorDialogAsync("Background Thread Fault", args.Exception));
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is not Exception ex) return;

            LogExtensions.LogFatal(ex, "Critical core crash - terminating.", this);
        };
    }

    private ServiceCollection ConfigureServices(IConfigurationRoot configuration)
    {
        var services = new ServiceCollection();
        services.AddOptions<ClientAdapterOptions>().Bind(configuration.GetSection(nameof(ClientAdapterOptions))).ValidateDataAnnotations();
        services.AddOptions<ApplicationSettings>().Bind(configuration.GetSection(nameof(ApplicationSettings))).ValidateDataAnnotations();
        services.AddSingleton<IScoutDataCoordTransformation, Wgs84ToKrovakTransformator>();
        services.AddSingleton<IDronetagClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ClientAdapterOptions>>();
            if (string.IsNullOrEmpty(options.Value.ClientSourceLogFilePath))
            {
                LogExtensions.LogInfo("Initializing MQTT client.", this);
                return new ScoutDataMqttClientAdapter(sp.GetRequiredService<IScoutDataCoordTransformation>(), options);
            }

            LogExtensions.LogInfo("Initializing log file client.", this);
            return new LogfileDronetagClient(options, sp.GetRequiredService<IScoutDataCoordTransformation>(), sp.GetRequiredService<IErrorDialogService>());
        });
        services.AddSingleton<IErrorDialogService>(_ => _errorDialogService);
        services.AddSingleton<MainViewViewModel>();
        return services;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}