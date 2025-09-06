using System.Timers;
using Mapsui;
using SmasKunovice.Avalonia.Models;
using SmasKunovice.Avalonia.Models.FakeClient;

namespace SmasKunovice.Avalonia.Tests.Models.Mapsui;

public class TestDroneTagClient : FakeDronetagClient
{
    private readonly Random _random = new();
    private int _counter = 0;
    private int _xMin = -541518;
    private int _xMax = -535341;
    private int _yMin = -1188872;
    private int _yMax = -1182974;
    private int _maxYShift;
    private int _maxXShift;
    private int _maxMessages = 5;
    private readonly List<ScoutData> _currentMessages;

    public TestDroneTagClient(int? intervalMs = null) : base(intervalMs)
    {
        // Each object should move only one tenth of the extent at max
        _maxYShift = (Math.Abs(_yMax) - Math.Abs(_yMin)) / 10;
        _maxXShift = (Math.Abs(_xMax) - Math.Abs(_xMin)) / 10;
        _currentMessages = [GenerateRandomMessage(), GenerateRandomMessage()];
    }

    public MRect GetExtent()
    {
        return new MRect(_xMin, _yMin, _xMax, _yMax).Grow(1000);
    }

    protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Timer is no longer used for message generation
        // This method can be left empty or removed if the base class allows it
    }

    /// <summary>
    /// Sends two messages for two defined features and returns them
    /// </summary>
    public void SendTwoMessages()
    {
        // Update existing messages with new positions
        for (var i = 0; i < _currentMessages.Count; i++)
        {
            var oldMessage = _currentMessages[i];
            _currentMessages[i] = GenerateRandomMessageWithId(
                oldMessage.Odid.BasicId[0].UasId,
                oldMessage.Odid.Location!.Latitude!.Value,
                oldMessage.Odid.Location.Longitude!.Value);
        }

        var args = new ScoutDataReceivedEventArgs
        {
            Messages = new List<ScoutData>(_currentMessages)
        };

        SendMessageReceived(args);
    }

    /// <summary>
    /// Gets the current messages without generating new ones
    /// </summary>
    public List<ScoutData> GetCurrentMessages()
    {
        return _currentMessages.ToList();
    }

    /// <summary>
    /// Gets the current number of active messages
    /// </summary>
    public int CurrentMessageCount => _currentMessages.Count;

    private ScoutData GenerateRandomMessage()
    {
        _counter++;
        var id = _counter;
        var latitude = _random.NextDouble() * (_xMax - _xMin) + _xMin;
        var longitude = _random.NextDouble() * (_yMax - _yMin) + _yMin;
        return GenerateRandomMessageWithId(id.ToString(), (float)latitude, (float)longitude);
    }

    private ScoutData GenerateRandomMessageWithId(string id, float prevLatitude, float prevLongitude)
    {
        // Generate shift (positive or negative) based on max shift
        var latitude = Math.Clamp(prevLatitude + (_random.Next(2) == 0 ? -1 : 1) * _random.NextDouble() * _maxXShift, _xMin, _xMax);
        var longitude = Math.Clamp(prevLongitude + (_random.Next(2) == 0 ? -1 : 1) * _random.NextDouble() * _maxYShift, _yMin, _yMax);

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