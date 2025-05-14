using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;

namespace SmasKunovice.Avalonia.Models;

public class DynamicPlanePositionProvider: MemoryProvider, IDynamic,  IDisposable
{
    public event DataChangedEventHandler? DataChanged;
    private readonly IDroneTagClient _client;
    private IEnumerable<DroneTagMessage>? _latestMessageData;

    public DynamicPlanePositionProvider(IDroneTagClient client)
    {
        _client = client;
        client.MessageReceived += ClientOnMessageReceived;
    }

    private void ClientOnMessageReceived(object sender, DroneTagMessageReceivedEventArgs e)
    {
        _latestMessageData = e.Messages;
        DataHasChanged();
    }

    public void DataHasChanged()
    {
        DataChanged?.Invoke(this, new DataChangedEventArgs());
    }

    public override Task<IEnumerable<IFeature>> GetFeaturesAsync(FetchInfo fetchInfo)
    {
        _latestMessageData ??= new List<DroneTagMessage>();
        return Task.FromResult<IEnumerable<IFeature>>(_latestMessageData.Select(m => m.ToPointFeature()));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client.Dispose();
        }
    }
}