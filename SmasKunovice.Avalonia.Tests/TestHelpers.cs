using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using SmasKunovice.Avalonia.Models.Config;

namespace SmasKunovice.Avalonia.Tests;

public static class TestHelpers
{
    public static IOptions<ClientAdapterOptions> CreateClientAdapterOptions()
    {
        var appSettingsBasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appSettingsBasePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<App>()
            .Build();
        var clientAdapterOptions = configuration.GetSection(nameof(ClientAdapterOptions)).Get<ClientAdapterOptions>();
        Assert.That(clientAdapterOptions, Is.Not.Null);
        var mockOptions = new Mock<IOptions<ClientAdapterOptions>>();
        mockOptions.Setup(o => o.Value).Returns(clientAdapterOptions);
        return mockOptions.Object;
    }
}