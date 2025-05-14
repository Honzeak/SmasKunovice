using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Logging;
using Mapsui.ArcGIS;
using Mapsui.ArcGIS.DynamicProvider;
using Mapsui.Cache;
using Mapsui.Layers;

namespace SmasKunovice.Avalonia.Models;

public static class ZtmDynamicLayerFactory
{
    
    private const string ZtmBaseRestUrl = @"https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/{{ZTM_DATASET}}/MapServer";
    // private static Dictionary<ZtmDatasets, IUrlPersistentCache> _urlDatasetCache = new(); // Do I need this?
    
    public static ImageLayer CreateDynamicLayer(ZtmDatasets ztmDataset)
    {
        var url = ZtmBaseRestUrl.Replace("{{ZTM_DATASET}}", ztmDataset.ToString());
        IUrlPersistentCache? defaultCache = null;
        var capabilitiesHelper = new CapabilitiesHelper(defaultCache);
        
        var capabilitiesTask = new TaskCompletionSource<ArcGISDynamicCapabilities>();
        capabilitiesHelper.CapabilitiesReceived += (sender, args) =>
        {
            if (sender is ArcGISDynamicCapabilities capabilities)
            {
                Logger.Sink!.Log(LogEventLevel.Information, LogArea.Control, null, "Got capabilities");
                capabilitiesTask.TrySetResult(capabilities);
            }
            else
                capabilitiesTask.TrySetException(new InvalidOperationException("Failed to get valid capabilities"));
        };
        
        Logger.Sink!.Log(LogEventLevel.Information, LogArea.Control, null, url);
        capabilitiesHelper.GetCapabilities(url, CapabilitiesType.DynamicServiceCapabilities);
        
        var completedTask = Task.WhenAny(capabilitiesTask.Task, Task.Delay(TimeSpan.FromSeconds(10))).Result;
        if (capabilitiesTask.Task.IsCompleted == false)
        {
            Logger.Sink!.Log(LogEventLevel.Fatal, LogArea.Control, null, "Timeout while getting capabilities");
            throw new TimeoutException("Timeout while getting capabilities");
        }
        
        // _urlDatasetCache[ztmDataset] = defaultCache!;
        var capabilities = capabilitiesTask.Task.Result;
        var provider = new ArcGISDynamicProvider(url, capabilities, null, defaultCache){CRS = "EPSG:5514"};
        
        return new ImageLayer(ztmDataset.ToString()) { DataSource = provider };
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum ZtmDatasets
{
    ZTM5,
    ZTM10,
    ZTM25,
    ZTM50,
    ZTM100
}