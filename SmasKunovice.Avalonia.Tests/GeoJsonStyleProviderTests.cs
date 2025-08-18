using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class GeoJsonStyleProviderTests
{
    const string geoJsonDataPath = @"C:\Users\honza\codes\SmasKunovice\SmasKunovice.Avalonia\Assets\GeoJsonElements\";

    [Test]
    public void TestGeoJsonStyleProvider_Init()
    {
        var provider = new GeoJsonStyleProvider(geoJsonDataPath);
        provider.Initialize();
        Assert.That(provider.IsInitialized, Is.True);
    }
}