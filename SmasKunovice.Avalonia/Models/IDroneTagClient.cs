using System;
using System.Collections.Generic;

namespace SmasKunovice.Avalonia.Models;

public interface IDroneTagClient : IDisposable
{
    public delegate void DronetagDataReceivedEventHandler(object sender, ScoutDataReceivedEventArgs e);

    public event DronetagDataReceivedEventHandler? MessageReceived;
}

public class ScoutDataReceivedEventArgs : EventArgs
{
    public required List<ScoutData> Messages { get; init; }
}