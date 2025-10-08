using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public class LogfileDronetagClient : FakeDronetagClient
{
    private int _currentIndex = 0;
    private readonly List<ScoutData> _messages;
    private readonly KrovakTransformator? _transformator;

    public LogfileDronetagClient(string logFilePath, int intervalMs, KrovakTransformator? transformator = null) : base(intervalMs)
    {
        if (!File.Exists(logFilePath))
            throw new FileNotFoundException($"Log file '{logFilePath}' not found.");

        _transformator = transformator;
        _messages = ParseJsonLog(logFilePath);
    }

    private List<ScoutData> ParseJsonLog(string logFilePath)
    {
        List<ScoutData>? messages;
        try
        {
            messages = JsonSerializer.Deserialize<List<ScoutData>>(File.ReadAllText(logFilePath), ScoutData.SerializerOptions)?.Select(scoutData => _transformator?.TransformScoutDataCoords(scoutData) ?? scoutData).ToList();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to parse JSON log file '{logFilePath}'.", e);
        }
        
        return messages!;
    }


    protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_messages.Count == 0)
            return;

        var scoutData = _messages[_currentIndex];
        SendMessageReceived(new ScoutDataReceivedEventArgs{ Messages = [scoutData] });
        _currentIndex = (_currentIndex + 1) % _messages.Count;
    }
}