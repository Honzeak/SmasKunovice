using System;
using System.Collections.Generic;
using System.Timers;
using MQTTnet;

namespace SmasKunovice.Avalonia.Models;

public class FakeDroneTagClient : IDroneTagClient
{
    public event IDroneTagClient.DronetagDataReceivedEventHandler? MessageReceived;
    private readonly Random _random = new();
    private int _counter = 0;
    private int _xMin = -541518;
    private int _xMax = -535341;
    private int _yMin = -1182974;
    private int _yMax = -1188872;
    private Timer _timer;
    private int _maxMessages = 10;
    private readonly List<ScoutData> _currentMessages = new();

    public FakeDroneTagClient()
    {
        _timer = new Timer(2000); // 2 seconds
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Enabled = true;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
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
            _currentMessages[i] = GenerateRandomMessageWithId(oldMessage.Odid.BasicId[0].UasId);
        }

        // Trigger the event if we have messages and subscribers
        if (_currentMessages.Count <= 0 || MessageReceived == null)
            return;

        var args = new ScoutDataReceivedEventArgs
        {
            Messages = new List<ScoutData>(_currentMessages)
        };

        MessageReceived.Invoke(this, args);
    }

    private ScoutData GenerateRandomMessage()
    {
        _counter++;
        var id = _counter;
        return GenerateRandomMessageWithId(id.ToString());
    }

    private ScoutData GenerateRandomMessageWithId(string id)
    {
        // Generate random latitude and longitude within the specified range
        var latitude = _random.NextDouble() * (_xMax - _xMin) + _xMin;
        var longitude = _random.NextDouble() * (_yMax - _yMin) + _yMin;

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

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        MessageReceived = null;
        GC.SuppressFinalize(this);
    }
}