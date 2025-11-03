using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Moq;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Tests.Models.Mapsui
{
    [TestFixture]
    public class DynamicScoutDataProviderTests : TestBase
    {
        private Mock<IDronetagClient> _mockClient;

        [SetUp]
        public void Setup()
        {
            _mockClient = new Mock<IDronetagClient>();
        }

        [Test]
        public void Constructor_RegistersForClientEvents()
        {
            // Act
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);

            // Assert
            _mockClient.VerifyAdd(
                c => c.MessageReceived += It.IsAny<IDronetagClient.DronetagDataReceivedEventHandler>());
        }

        [Test]
        public async Task GetFeaturesAsync_WithNoMessages_ReturnsEmptyCollection()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);

            // Act
            var features = await provider.GetFeaturesAsync(new FetchInfo(new MSection(new MPoint(1, 2).MRect, 1)));
            features = features.ToList();

            // Assert
            Assert.That(features, Is.Not.Null);
            Assert.That(features.Any(), Is.False);
        }

        [Test]
        public async Task GetFeaturesAsync_AfterMessageReceived_ReturnsFeatures()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);
            var messages = new List<ScoutData>
            {
                CreateScoutData("Drone1", -538000, -1185000, 45, 120),
                CreateScoutData("Drone2", -538100, -1185100, 50, 130)
            };

            var eventArgs = new ScoutDataReceivedEventArgs
            {
                Messages = messages
            };

            // Simulate message received event
            _mockClient.Raise(c => c.MessageReceived += null, _mockClient.Object, eventArgs);

            // Act
            var features = await provider.GetFeaturesAsync(new FetchInfo(new MSection(new MPoint(1, 2).MRect, 1)));
            var featuresList = features.ToList();

            // Assert
            Assert.That(featuresList, Is.Not.Null);
            Assert.That(featuresList, Has.Count.EqualTo(2));
            using (Assert.EnterMultipleScope())
            {
                // Verify that features match the messages
                Assert.That(featuresList[0], Is.InstanceOf<PointFeature>());
                Assert.That(featuresList[1], Is.InstanceOf<PointFeature>());
            }

            var drone1Feature = (PointFeature)featuresList[0];
            var drone2Feature = (PointFeature)featuresList[1];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(drone1Feature[ScoutData.FeatureScoutDataField], Is.Not.Null.And.InstanceOf<ScoutData>());
                Assert.That(drone2Feature[ScoutData.FeatureScoutDataField], Is.Not.Null.And.InstanceOf<ScoutData>());
            }

            var message1 = (ScoutData)drone1Feature[ScoutData.FeatureScoutDataField]!;
            var message2 = (ScoutData)drone2Feature[ScoutData.FeatureScoutDataField]!;
            Assert.That(message1, Is.Not.Null);
            Assert.That(message2, Is.Not.Null);
            
            using (Assert.EnterMultipleScope())
            {
                Assert.That(message1.Odid.BasicId[0].UasId, Is.EqualTo("Drone1"));
                Assert.That(message1.Odid.Location, Is.Not.Null);
                Assert.That(message1.Odid.Location.Direction, Is.EqualTo(45));
                Assert.That(message2.Odid.BasicId[0].UasId, Is.EqualTo("Drone2"));
                Assert.That(message2.Odid.Location, Is.Not.Null);
                Assert.That(message2.Odid.Location.Direction, Is.EqualTo(50));
            }
        }

        [Test]
        public void DataHasChanged_TriggersDataChangedEvent()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);
            var eventWasRaised = false;
            DataChangedEventArgs capturedArgs = null;

            provider.DataChanged += (sender, args) =>
            {
                eventWasRaised = true;
                capturedArgs = args;
            };

            // Act
            provider.DataHasChanged();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(eventWasRaised, Is.True);
                Assert.That(capturedArgs, Is.Not.Null);
            }
        }

        [Test]
        public void MessageReceived_TriggersDataChangedEvent()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);
            var eventWasRaised = false;

            provider.DataChanged += (sender, args) => eventWasRaised = true;

            var messages = new List<ScoutData>
            {
                CreateScoutData("Drone1", -538000, -1185000, 45, 120)
            };

            var eventArgs = new ScoutDataReceivedEventArgs
            {
                Messages = messages
            };

            // Act
            _mockClient.Raise(c => c.MessageReceived += null, _mockClient.Object, eventArgs);

            // Assert
            Assert.That(eventWasRaised, Is.True);
        }

        [Test]
        public void Dispose_DisposesClient()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);

            // Act
            provider.Dispose();

            // Assert
            _mockClient.Verify(c => c.Dispose(), Times.Once);
        }

        [Test]
        public async Task GetFeaturesAsync_UpdatesWithNewMessages()
        {
            // Arrange
            using var provider = new DynamicScoutDataProvider(_mockClient.Object);

            var firstMessages = new List<ScoutData>
            {
                CreateScoutData("Drone1", -538000, -1185000, 45, 120)
            };

            var firstEventArgs = new ScoutDataReceivedEventArgs
            {
                Messages = firstMessages
            };

            // Simulate first message
            _mockClient.Raise(c => c.MessageReceived += null, _mockClient.Object, firstEventArgs);

            // Act - First check
            var firstFeatures =
                (await provider.GetFeaturesAsync(new FetchInfo(new MSection(new MPoint(1, 2).MRect, 1)))).ToList();

            // Now simulate a second message
            var secondMessages = new List<ScoutData>
            {
                CreateScoutData("Drone1", -538100, -1185100, 50, 130),
                CreateScoutData("Drone2", -538200, -1185200, 55, 140)
            };

            var secondEventArgs = new ScoutDataReceivedEventArgs
            {
                Messages = secondMessages
            };

            _mockClient.Raise(c => c.MessageReceived += null, _mockClient.Object, secondEventArgs);

            // Act - Second check
            var secondFeatures =
                (await provider.GetFeaturesAsync(new FetchInfo(new MSection(new MPoint(1, 2).MRect, 1)))).ToList();

            // Assert
            Assert.That(firstFeatures, Has.Count.EqualTo(1));
            Assert.That(secondFeatures, Has.Count.EqualTo(2));
            Assert.That(firstFeatures, Has.All.InstanceOf<PointFeature>());
            Assert.That(secondFeatures, Has.All.InstanceOf<PointFeature>());

            var droneTagMessage = firstFeatures[0][ScoutData.FeatureScoutDataField] as ScoutData;
            Assert.That(droneTagMessage, Is.Not.Null);
            Assert.That(droneTagMessage.Odid.BasicId[0].UasId, Is.EqualTo("Drone1"));

            droneTagMessage = secondFeatures[0][ScoutData.FeatureScoutDataField] as ScoutData;
            Assert.That(droneTagMessage, Is.Not.Null);
            Assert.That(droneTagMessage.Odid.Location, Is.Not.Null);
            Assert.That(droneTagMessage.Odid.Location.Latitude, Is.EqualTo(-538100));
            Assert.That(droneTagMessage.Odid.Location.Longitude, Is.EqualTo(-1185100));
        }
        
        private static ScoutData CreateScoutData(string id, float latitude, float longitude, int heading, float speed)
        {
            return new ScoutData
            {
                Odid = new OdidData
                {
                    BasicId = [new BasicIdData { UasId = id }],
                    Location = new LocationData
                    {
                        Latitude = (float)latitude, Longitude = (float)longitude, Direction = (int)heading,
                        SpeedHorizontal = (float)speed
                    }
                }
            };
        }
    }
}