using System.Threading.Tasks;
using System.Timers;

namespace SmasKunovice.Avalonia.Models.FakeClient;

/// <summary>
/// Represents an abstract base class for a fake DroneTag client implementation.
/// Contains functionality for periodically sending simulated scout data.
/// This class simulates the behavior of a real DroneTag client for testing and development purposes.
/// </summary>
public abstract class PeriodicDronetagClient : FakeDronetagClient
{
    private readonly Timer _timer;
    private bool _disposedDerived;
    private const int DefaultIntervalMs = 2000; // ms

    protected PeriodicDronetagClient(int? intervalMs = null)
    {
        _timer = new Timer(intervalMs ?? DefaultIntervalMs);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
    }

    public override Task ConnectAsync()
    {
        _timer.Start();
        return base.ConnectAsync();
    }

    protected abstract void OnTimerElapsed(object? sender, ElapsedEventArgs e);

    protected override void Dispose(bool disposing)
    {
        if (_disposedDerived)
            return;

        if (disposing)
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        _disposedDerived = true;
    }
}