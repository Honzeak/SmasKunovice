using Microsoft.Extensions.Configuration;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Config;

namespace SmasKunovice.Avalonia.Tests.Integration;

[TestFixture]
public class AircraftDatabaseIntegrationTests : TestBase
{
    [Test]
    public void LoadDatabase()
    {
        var databasePath = Directory.EnumerateFiles(AssetProvider.GetFullAssetPath("Database"), "*.csv", SearchOption.TopDirectoryOnly).Single();
        var aircraftDatabase = new AircraftDatabase(databasePath);
        var record = aircraftDatabase.GetByIcao24("c81004");
        Assert.That(record, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Model, Is.EqualTo("Sport"));
            Assert.That(record.Registration, Is.EqualTo("ZK-RBW"));
        }
    }
    
}