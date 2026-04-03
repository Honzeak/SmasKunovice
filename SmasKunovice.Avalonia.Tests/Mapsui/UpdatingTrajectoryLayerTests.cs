using Mapsui;
using Mapsui.Layers;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.Mapsui
{
    [TestFixture]
    public class UpdatingTrajectoryLayerTests
    {
        [Test]
        public void Layer_ReturnsFeaturesOnUpdate()
        {
            // Arrange
            var (fakeClient, layer) = InitLayer();
            layer.ObservableQueueSize = 3;
            Assert.That(layer.GetFeatures(TestDroneTagClient.GetExtent(), 1), Is.Empty);
            fakeClient.SendNewMessage();
            Assert.That(layer.GetFeatures(TestDroneTagClient.GetExtent(), 1), Is.Empty);
            fakeClient.SendNewMessage();
            Assert.That(layer.GetFeatures(TestDroneTagClient.GetExtent(), 1), Is.Not.Empty);
        }

        [Test]
        public void Layer_ReturnsCorrectNumberOfFeatures_And_CorrectOrder()
        {
            var (fakeClient, layer) = InitLayer();
            var sentCount = 0;
            List<PointFeature> firstMessages = [];
            while (sentCount != 4)
            {
                sentCount++;
                fakeClient.SendNewMessage();
                if (sentCount != 1)
                    continue;
                firstMessages = fakeClient.GetCurrentMessage().Select(m =>
                {
                    m.TryCreatePointFeature(out var pointFeature);
                    return pointFeature;
                }).Where(f => f is not null).ToList()!;
                Assert.That(firstMessages, Has.Count.EqualTo(1));
            }

            layer.ObservableQueueSize = 2;
            var features = layer.GetFeatures(TestDroneTagClient.GetExtent(), 1).Cast<PointFeature>().ToList();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(features, Has.Count.EqualTo(2));
                Assert.That(CheckExistenceOfFirstMessages(features, firstMessages), Is.False);
            }
            layer.ObservableQueueSize = 3;
            features = layer.GetFeatures(TestDroneTagClient.GetExtent(), 1).Cast<PointFeature>().ToList();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(features, Has.Count.EqualTo(3));
                Assert.That(CheckExistenceOfFirstMessages(features, firstMessages), Is.True);
            }
        }

        private static bool CheckExistenceOfFirstMessages(List<PointFeature> features, List<PointFeature> firstMessages)
        {
            // Return true only when all firstMessages elements are found in features
            // Comparison is based on ID field and X, Y coordinates
            return firstMessages.All(firstMessage =>
                features.Any(feature =>
                    // Compare ID field
                    Equals(feature[ScoutData.FeatureUasIdField], firstMessage[ScoutData.FeatureUasIdField]) &&
                    // Compare X coordinate (with tolerance for floating point comparison)
                    Math.Abs(feature.Point.X - firstMessage.Point.X) < 1e-10 &&
                    // Compare Y coordinate (with tolerance for floating point comparison)
                    Math.Abs(feature.Point.Y - firstMessage.Point.Y) < 1e-10
                )
            );
        }

        private static (TestDroneTagClient fakeClient, UpdatingTrajectoryLayer layer) InitLayer()
        {
            var fakeClient = new TestDroneTagClient();
            var dataProvider = new DynamicScoutDataProvider(fakeClient);
            var layer = new UpdatingTrajectoryLayer(dataProvider);
            layer.RefreshData(new FetchInfo(new MSection(TestDroneTagClient.GetExtent(), 1)));
            return (fakeClient, layer);
        }
    }
}