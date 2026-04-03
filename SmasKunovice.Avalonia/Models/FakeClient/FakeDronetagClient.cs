using System;
using System.Threading.Tasks;

namespace SmasKunovice.Avalonia.Models.FakeClient;

public abstract class FakeDronetagClient : IDronetagClient
{
    private bool _disposed;
    public event IDronetagClient.DronetagDataReceivedEventHandler? MessageReceived;

    public virtual Task ConnectAsync()
    {
        Console.WriteLine("Connected to fake drone tag client.");
        return Task.CompletedTask;
    }

    protected bool SendMessageReceived(ScoutDataReceivedEventArgs e)
    {
        if (MessageReceived is null)
            return false;

        MessageReceived.Invoke(this, e);
        return true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            MessageReceived = null;

        _disposed = true;
    }
}