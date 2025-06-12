using System;
using System.Threading.Tasks;
using System.Timers;

namespace SmasKunovice.Avalonia.Models.FakeClient;
/// <summary>
/// Represents an abstract base class for a fake DroneTag client implementation.
/// Contains functionality for periodically sending simulated scout data.
/// This class simulates the behavior of a real DroneTag client for testing and development purposes.
/// </summary>
public abstract class FakeDronetagClient : IDronetagClient
{
    private Timer _timer;

    protected FakeDronetagClient(int intervalMs)
    {
        _timer = new Timer(intervalMs); // 2 seconds
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
    }

    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;

    public async Task ConnectAsync()
    {
        _timer.Start();
        Console.WriteLine("Connected to fake drone tag client.");
        await Task.CompletedTask;
    }

    protected abstract void OnTimerElapsed(object? sender, ElapsedEventArgs e);

    protected bool SendMessageReceived(ScoutDataReceivedEventArgs e)
    {
        if (MessageReceived is null)
            return false;
        
        MessageReceived.Invoke(this, e);
        return true;
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        MessageReceived = null;
        GC.SuppressFinalize(this);
    }
}