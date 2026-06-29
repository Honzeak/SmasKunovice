using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SmasKunovice.Avalonia.Extensions;
using SmasKunovice.Avalonia.Models.Mapsui;

namespace SmasKunovice.Avalonia.Models;

public class ConflictDetectionService(DynamicScoutDataProvider scoutDataProvider, IErrorDialogService dialogService, DroneGridIntersectionDetector droneDetector, RpaPresenceConflictDetector rpaDetector, RunwayApproachConflictDetector approachDetector) : IDisposable
{
    private bool _disposed;
    private readonly ConcurrentDictionary<ConflictKey, ConflictFeature> Features = new();
    
    public async Task InitializeAsync()
    {
        await scoutDataProvider.ConnectClientAsync();
        scoutDataProvider.DataChanged += OnProviderDataChanged;
    }
    
        private async void OnProviderDataChanged(object? sender, EventArgs e)
    {
        try
        {
            await UpdateDataAsync();
        }
        catch (Exception exception)
        {
            LogExtensions.LogError(exception, "Error updating layer");
            Dispose();
            await dialogService.ShowErrorDialogAsync("Error updating layer", exception);
        }
    }

    private async Task UpdateDataAsync()
    {
        var latestMessageData = await scoutDataProvider.GetFeaturesAsync();
        foreach (var pointFeature in latestMessageData)
        {
            Features[]
        }

    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        scoutDataProvider.DataChanged -= OnProviderDataChanged;
        _disposed = true;
    }
}

public readonly record struct ConflictKey(string Id, ConflictType Type)
{
}

public enum ConflictType
{
    DroneGridIntersection,
    RpaPresence,
    RunwayApproach
}