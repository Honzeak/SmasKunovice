using System.Timers;
using Mapsui;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;

namespace SmasKunovice.Avalonia.Tests.Models.Mapsui;

public class TestDroneTagClient : FakeDronetagClient
{
    private readonly Random _random = new();
    private int _counter = 0;
    private static int _xMin = -541518;
    private static int _xMax = -535341;
    private static int _yMin = -1188872;
    private static int _yMax = -1182974;
    private int _maxYShift;
    private int _maxXShift;
    private int _maxMessages = 5;
    private readonly List<ScoutData> _currentMessage;

    public TestDroneTagClient(int? intervalMs = null) : base(intervalMs)
    {
        // Each object should move only one tenth of the extent at max
        _maxYShift = (Math.Abs(_yMax) - Math.Abs(_yMin)) / 10;
        _maxXShift = (Math.Abs(_xMax) - Math.Abs(_xMin)) / 10;
        _currentMessage = [GenerateRandomMessage()];
    }

    public static MRect GetExtent()
    {
        return new MRect(_xMin, _yMin, _xMax, _yMax).Grow(1000);
    }

    protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Timer is no longer used for message generation
        // This method can be left empty or removed if the base class allows it
    }

    /// <summary>
    /// Sends a message a defined feature
    /// </summary>
    public void SendNewMessage()
    {
        // Update existing messages with new positions
        var oldMessage = _currentMessage[0];
        _currentMessage[0] = GenerateRandomMessageWithId(
            oldMessage.Odid.BasicId[0].UasId,
            oldMessage.Odid.Location!.Latitude!.Value,
            oldMessage.Odid.Location.Longitude!.Value);


        var args = new ScoutDataReceivedEventArgs
        {
            Messages = new List<ScoutData>(_currentMessage)
        };

        SendMessageReceived(args);
    }

    /// <summary>
    /// Gets the current messages without generating new ones
    /// </summary>
    public List<ScoutData> GetCurrentMessage()
    {
        return _currentMessage.ToList();
    }

    private ScoutData GenerateRandomMessage()
    {
        _counter++;
        var id = _counter;
        var latitude = _random.NextDouble() * (_yMax - _yMin) + _yMin;
        var longitude = _random.NextDouble() * (_xMax - _xMin) + _xMin;
        return GenerateRandomMessageWithId(id.ToString(), (float)latitude, (float)longitude);
    }

    private ScoutData GenerateRandomMessageWithId(string id, float prevLatitude, float prevLongitude)
    {
        // Generate shift (positive or negative) based on max shift
        var latitude = Math.Clamp(prevLatitude + (_random.Next(2) == 0 ? -1 : 1) * _random.NextDouble() * _maxXShift,
            _yMin, _yMax);
        var longitude = Math.Clamp(prevLongitude + (_random.Next(2) == 0 ? -1 : 1) * _random.NextDouble() * _maxYShift,
            _xMin, _xMax);

        // Generate other random properties
        var speed = _random.NextDouble() * 200; // Speed between 0 and 200
        var heading = _random.NextDouble() * 360; // Heading between 0 and 360

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