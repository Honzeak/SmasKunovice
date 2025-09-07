using System;
using System.Collections.Generic;
using System.Timers;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public class RandomMessageDronetagClient : FakeDronetagClient
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
    private readonly List<ScoutData> _currentMessages = new();

    public RandomMessageDronetagClient(int? intervalMs = null) : base(intervalMs)
    {
        // Each object should move only one tenth of the extent at max
        _maxYShift = (Math.Abs(_yMax) - Math.Abs(_yMin)) / 10;
        _maxXShift = (Math.Abs(_xMax) - Math.Abs(_xMin)) / 10;
    }

    protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Decide whether to increase, decrease, or keep the same number of messages
        // 60% chance to increase, 30% chance to stay the same, 10% chance to decrease
        var changeType = _random.Next(10);
        var currentCount = _currentMessages.Count;

        var targetCount = changeType switch
        {
            // 60% chance to increase
            < 6 => Math.Min(currentCount + 1, _maxMessages),
            // 30% chance to stay the same
            < 9 => currentCount,
            _ => Math.Max(currentCount - 1, 0)
        };

        // Adjust the message list to match the target count
        if (targetCount > currentCount)
        {
            // Add new messages
            _currentMessages.Add(GenerateRandomMessage());
        }
        else if (targetCount < currentCount)
        {
            // Remove random messages
            var indexToRemove = _random.Next(_currentMessages.Count);
            _currentMessages.RemoveAt(indexToRemove);
        }

        // Update existing messages with new positions
        for (var i = 0; i < _currentMessages.Count; i++)
        {
            var oldMessage = _currentMessages[i];
            _currentMessages[i] = GenerateRandomMessageWithId(
                oldMessage.Odid.BasicId[0].UasId,
                oldMessage.Odid.Location!.Latitude!.Value,
                oldMessage.Odid.Location.Longitude!.Value);
        }

        // Trigger the event if we have messages and subscribers
        if (_currentMessages.Count <= 0)
            return;

        var args = new ScoutDataReceivedEventArgs
        {
            Messages = new List<ScoutData>(_currentMessages)
        };

        SendMessageReceived(args);
    }

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
        var speed = _random.NextDouble() * 50;
        var heading = _random.NextDouble() * 360;

        return new ScoutData
        {
            Odid = new OdidData
            {
                BasicId = [new BasicIdData { UasId = id }],
                Location = new LocationData
                {
                    Latitude = (float)latitude,
                    Longitude = (float)longitude,
                    Direction = (int)heading,
                    SpeedHorizontal = (float)speed
                }
            }
        };
    }
}