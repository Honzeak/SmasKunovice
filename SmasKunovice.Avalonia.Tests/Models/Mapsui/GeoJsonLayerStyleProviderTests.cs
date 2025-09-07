using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Moq;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.Models.Mapsui;

[TestFixture]
public class GeoJsonLayerStyleProviderTests : TestBase
{
    private const string GeoJsonDataPath = $"TestData/{nameof(GeoJsonLayerStyleProviderTests)}";

    [Test]
    public void ExtractLayerProperties_Exist()
    {
        var mockFeature = new Mock<IFeature>();
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        var layerProperty = provider.GeoJsonLayerProperties.SingleOrDefault(p => p.Name.Equals("Ctr3"));
        Assert.That(layerProperty, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            var actual = ((ThemeStyle)layerProperty.Style).GetStyle(mockFeature.Object) as VectorStyle;
            Assert.That(actual, Is.Not.Null.And.TypeOf<VectorStyle>());
            Assert.That(actual.Fill?.Color, Is.EqualTo(Color.Yellow));
            Assert.That(layerProperty.Opacity, Is.EqualTo(0.4f));
            Assert.That(layerProperty.Order, Is.EqualTo(2));
        }
    }

    [Test]
    public void ExtractLayerProperties_Default()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        var layerProperty = provider.GeoJsonLayerProperties.SingleOrDefault(p => p.Name.Equals("Ctr3_noProps"));
        Assert.That(layerProperty, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(layerProperty.Opacity, Is.EqualTo(1));
            Assert.That(layerProperty.Order, Is.EqualTo(0));
        }
    }

    [Test]
    public void ExtractLayerProperties_NotExists()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        var layerProperty = provider.GeoJsonLayerProperties.SingleOrDefault(p => p.Name.Equals("Ctr3_file_not_exists"));
        Assert.That(layerProperty, Is.Null);
    }
}