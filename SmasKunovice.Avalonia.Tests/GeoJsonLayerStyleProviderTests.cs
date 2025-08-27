using Mapsui.Styles;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests;

[TestFixture]
public class GeoJsonLayerStyleProviderTests : TestBase
{
    private const string GeoJsonDataPath = $"TestData/{nameof(GeoJsonLayerStyleProviderTests)}";

    [Test]
    public void TestGeoJsonStyleProvider_Init()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        provider.Initialize();
        Assert.That(provider.IsInitialized, Is.True);
    }

    [Test]
    public void ExtractLayerProperties_Exist()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        provider.Initialize();
        var success = provider.LayerProperties.TryGetValue("Ctr3", out var layerProperty);
        Assert.That(success, Is.True);
        Assert.That(layerProperty, Is.Not.Null);
        Assert.That(layerProperty.Color, Is.EqualTo(Color.Yellow));
        Assert.That(layerProperty.Opacity, Is.EqualTo(0.4f));
        Assert.That(layerProperty.Order, Is.EqualTo(2));
    }

    [Test]
    public void ExtractLayerProperties_Default()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        provider.Initialize();
        var success = provider.LayerProperties.TryGetValue("Ctr3_noProps", out var layerProperty);
        Assert.That(success, Is.True);
        Assert.That(layerProperty, Is.Not.Null);
        Assert.That(layerProperty.Opacity, Is.EqualTo(1));
        Assert.That(layerProperty.Order, Is.EqualTo(0));
    }

    [Test]
    public void ExtractLayerProperties_NotExists()
    {
        var provider = new GeoJsonLayerStyleProvider(GeoJsonDataPath);
        provider.Initialize();
        var success = provider.LayerProperties.TryGetValue("Ctr3_file_not_exists", out _);
        Assert.That(success, Is.False);
    }
}