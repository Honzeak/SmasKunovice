using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmasKunovice.Avalonia.Models;

public interface IDronetagClient : IDisposable
{
    public delegate void DronetagDataReceivedEventHandler(object sender, ScoutDataReceivedEventArgs e);

    public event DronetagDataReceivedEventHandler? MessageReceived;
    public Task ConnectAsync();
}

public class ScoutDataReceivedEventArgs : EventArgs
{
    public required List<ScoutData> Messages { get; init; }
}