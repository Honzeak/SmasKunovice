using System;
using System.Collections.Generic;

namespace SmasKunovice.Avalonia.Models;

public interface IDroneTagClient : IDisposable
{
    public delegate void DronetagDataReceivedEventHandler(object sender, DroneTagMessageReceivedEventArgs e);

    public event DronetagDataReceivedEventHandler? MessageReceived;
}

public class DroneTagMessageReceivedEventArgs : EventArgs
{
    public required IEnumerable<DroneTagMessage> Messages { get; init; }
}