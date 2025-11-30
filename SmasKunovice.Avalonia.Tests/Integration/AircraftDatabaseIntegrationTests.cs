using Microsoft.Extensions.Configuration;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Config;

namespace SmasKunovice.Avalonia.Tests.Integration;

[TestFixture]
public class AircraftDatabaseIntegrationTests : TestBase
{
    [Test]
    public void LoadDatabaseFromAppsettings()
    {
        var appSettingsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true) // Load appsettings.json
            .Build();
        var configSection = configuration.GetSection(nameof(ApplicationSettings));
        var appSettings = configSection.Get<ApplicationSettings>();
        Assert.That(appSettings?.AircraftDatabasePath, Is.Not.Null.Or.Empty);

        var aircraftDatabase = new AircraftDatabase(appSettings.AircraftDatabasePath);
        var record = aircraftDatabase.GetByIcao24("c81004");
        Assert.That(record, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Model, Is.EqualTo("Sport"));
            Assert.That(record.Registration, Is.EqualTo("ZK-RBW"));
        }
    }
    
}