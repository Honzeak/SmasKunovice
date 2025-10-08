using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Timers;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public class LogfileDronetagClient : FakeDronetagClient
{
    private readonly List<ScoutData> _messages;

    public LogfileDronetagClient(string logFilePath, int intervalMs) : base(intervalMs)
    {
        if (!File.Exists(logFilePath))
            throw new FileNotFoundException($"Log file '{logFilePath}' not found.");
        
        _messages = ParseJsonLog(logFilePath);
    }

    private static List<ScoutData> ParseJsonLog(string logFilePath)
    {
        List<ScoutData>? messages;
        try
        {
            messages = JsonSerializer.Deserialize<List<ScoutData>>(File.ReadAllText(logFilePath), ScoutData.SerializerOptions);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to parse JSON log file '{logFilePath}'.", e);
        }
        
        return messages!;
    }

    private int _currentIndex = 0;

    protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_messages.Count == 0)
            return;

        var scoutData = _messages[_currentIndex];
        SendMessageReceived(new ScoutDataReceivedEventArgs{ Messages = [scoutData] });
        _currentIndex = (_currentIndex + 1) % _messages.Count;
    }
}