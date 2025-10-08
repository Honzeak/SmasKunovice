using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.ViewModels;
using SmasKunovice.Avalonia.Views;

namespace SmasKunovice.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // Load appsettings.json
            .AddUserSecrets<App>()
            .Build();
        
        var services = ConfigureServices(configuration);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var serviceProvider = services.BuildServiceProvider();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(serviceProvider.GetRequiredService<MainViewViewModel>()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ServiceCollection ConfigureServices(IConfigurationRoot configuration)
    {
        var services = new ServiceCollection();
        services.AddOptions<ClientAdapterOptions>().Bind(configuration.GetSection(nameof(ClientAdapterOptions))).ValidateDataAnnotations();
        // services.AddSingleton<IDroneTagClient, DronetagMqttClientAdapter>();
        // services.AddSingleton<IDronetagClient, RandomMessageDronetagClient>();
        services.AddSingleton<IDronetagClient>(sp => 
        {
            var filePath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia.Tests\TestData\LogFileDronetagClientTests\dronetag-odid-fix.json";
            return new LogfileDronetagClient(filePath, 1000);
        });
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