using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Logging;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;

namespace SmasKunovice.Avalonia.Models;

public class DynamicScoutDataProvider: MemoryProvider, IDynamic,  IDisposable
{
    public event DataChangedEventHandler? DataChanged;
    private readonly IDronetagClient _client;
    private List<ScoutData> _latestMessageData;

    public DynamicScoutDataProvider(IDronetagClient client)
    {
        _client = client;
        _latestMessageData = [];
        _client.MessageReceived += ClientOnMessageReceived;
        _client.ConnectAsync().Wait();
    }

    private void ClientOnMessageReceived(object sender, ScoutDataReceivedEventArgs e)
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
        var pointFeatures = _latestMessageData.Select(m =>
        {
            m.TryCreatePointFeature(out var pointFeature);
            return pointFeature;
        }).Where(f => f is not null).ToList();
        LogExtensions.LogInfo("Received {0} features from dronetag",this, pointFeatures.Count);
        return Task.FromResult<IEnumerable<IFeature>>(pointFeatures!);
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