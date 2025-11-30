using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class AircraftDatabaseTests : TestBase
{
    [Test]
    public void TestGetRegistration_CustomParser()
    {
        var csvPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", nameof(AircraftDatabaseTests), "database_sample.csv");
        var aircraftDb = new AircraftDatabase(csvPath);
        
        using (Assert.EnterMultipleScope())
        {
            var record1 = aircraftDb.GetByIcao24("0000A2");
            var record2 = aircraftDb.GetByIcao24("4ca579");
            Assert.That(record1, Is.Not.Null);
            Assert.That(record2, Is.Not.Null);

            Assert.That(record1!.Registration, Is.Null);
            Assert.That(record2!.Registration, Is.EqualTo("EI-GLG"));
            Assert.That(record2.Owner, Is.EqualTo("Playle, R dunkley, I sinclair, Ciaran"));
        }
    }
}