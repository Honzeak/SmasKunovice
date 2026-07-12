using Mapsui;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Moq;
using SmasKunovice.Avalonia.Models.ConflictResolution;
using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.Models.Mapsui;
using SmasKunovice.Avalonia.Tests.TestUtils;

namespace SmasKunovice.Avalonia.Tests.Mapsui;

[TestFixture]
public class MapLayerFactoryTests
{
    [Test]
    public void TestCreateAirportElementsLayers()
    {
        // Should load file ApronGuidanceLineMarking.geojson
        var layer = new MapLayerFactory(new Mock<IProvider>().Object, new DummyErrorDialogService(), new Mock<IConflictDetectionService>().Object)
            .CreateAirportElementsLayers(new GeoJsonLayerStyleProvider($"TestData\\{nameof(MapLayerFactoryTests)}"), out var procedureLayerNames)
            .Single();
        var featureMock = new Mock<IFeature>();
        Assert.That(layer.Style, Is.Not.Null.And.TypeOf<ThemeStyle>());
        var actualStyle = ((ThemeStyle)layer.Style).GetStyle(featureMock.Object) as VectorStyle ??
                          throw new Exception("Style is not a VectorStyle");
        Assert.That(actualStyle.Fill?.Color, Is.EqualTo(Color.Yellow));
    }
}