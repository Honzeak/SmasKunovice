using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Moq;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.Models.Mapsui;

[TestFixture]
public class MapLayerFactoryTests
{
    [Test]
    public void TestCreateAirportElementsLayers()
    {
        // Should load file ApronGuidanceLineMarking.geojson
        var layer = MapLayerFactory
            .CreateAirportElementsLayers(new GeoJsonLayerStyleProvider($"TestData\\{nameof(MapLayerFactoryTests)}"))
            .Single();
        var featureMock = new Mock<IFeature>();
        Assert.That(layer.Style, Is.Not.Null.And.TypeOf<ThemeStyle>());
        var actualStyle = ((ThemeStyle)layer.Style).GetStyle(featureMock.Object) as VectorStyle ??
                          throw new Exception("Style is not a VectorStyle");
        Assert.That(actualStyle.Fill?.Color, Is.EqualTo(Color.Yellow));
    }
}