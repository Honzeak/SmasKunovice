using System.Text.Json;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts.Providers;

namespace SmasKunovice.Avalonia.Tests.Mapsui;

[TestFixture]
public class GeoJsonProviderTest : TestBase
{
    [Test]
    [TestCase(1)]
    public void Test(double resolution)
    {
        var path = @"C:\Users\honza\codes\!SmasKunovice\SmasKunovice.Avalonia\Assets\GeoJsonElements\DroneGridCtr.geojson";
        var provider = new GeoJsonProvider(path);
        // TR = -520110 -1164093 | BL = -551711 -1201119
        var fetchInfo = new FetchInfo(new MSection(new MRect(-551711, -1201119, -520110, -1164093), resolution));
        var features = provider.GetFeaturesAsync(fetchInfo).GetAwaiter().GetResult();
        TestContext.Out.WriteLine(features.ToArray().Length);
    }
}