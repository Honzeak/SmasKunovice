using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Providers;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Dronetag;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class DynamicScoutDataProvider : MemoryProvider, IDynamic, IDisposable
{
    public event DataChangedEventHandler? DataChanged;
    private readonly IDronetagClient _client;
    private List<ScoutData> _latestMessageData = [];
    private bool _isConnected;

    public DynamicScoutDataProvider(IDronetagClient client)
    {
        _client = client;
        _client.MessageReceived += ClientOnMessageReceived;
    }

    public async Task ConnectClientAsync()
    {
        if (!_isConnected)
        {
            await _client.ConnectAsync();
            _isConnected = true;
        }
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

    public override async Task<IEnumerable<IFeature>> GetFeaturesAsync(FetchInfo fetchInfo)
    {
        if (!_isConnected)
        {
            LogExtensions.LogWarning("Dronetag client not connected while trying to fetch data.", this);
            return [];
        }

        var pointFeatures = _latestMessageData.Select(m =>
        {
            m.TryCreatePointFeature(out var pointFeature);
            return pointFeature;
        }).Where(f => f is not null).ToList();
        return await Task.FromResult<IEnumerable<IFeature>>(pointFeatures!);
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
            _client.MessageReceived -= ClientOnMessageReceived;
        }

        _client.Dispose();
    }
}