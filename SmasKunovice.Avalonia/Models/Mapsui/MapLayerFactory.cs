using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BruTile;
using BruTile.Cache;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Extent = BruTile.Extent;

namespace SmasKunovice.Avalonia.Models.Mapsui;

public class MapLayerFactory(DynamicScoutDataProvider dynamicScoutDataProvider, IErrorDialogService errorDialogService)
{
    private static readonly string RpaAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "Rpa.geojson"));
    private static readonly string ApproachZoneAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "ApproachProximityZone.geojson"));
    private static readonly string RunwayStartPointAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "runwayStartPoint.geojson"));
    private static readonly string DroneGridAssetPath = AssetProvider.GetFullAssetPath(Path.Combine("GeoJsonElements", "DroneGridCtr.geojson"));
    private static readonly Dictionary<string, IPersistentCache<byte[]>?> ZtmTileCache = new();
    private const string ZtmBaseRestUrl = "https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/{{ZTM_DATASET}}/MapServer/tile/{z}/{y}/{x}";
    public const string ProcedureLayerPrefix = "proc_";

    public static ILayer[] CreateZtmDynamicLayers(ZtmDatasets ztmDatasetFar, ZtmDatasets ztmDatasetNear)
    {
        var farLayer = CreateZtmTileLayer(ztmDatasetFar, minVisible: 8);
        var nearLayer = CreateZtmTileLayer(ztmDatasetNear, maxVisible: 8);
        return [farLayer, nearLayer];
    }

    private static TileLayer CreateZtmTileLayer(ZtmDatasets ztmDataset, int minVisible = 0, int maxVisible = int.MaxValue)
    {
        if (minVisible > maxVisible || minVisible < 0)
            throw new ArgumentException("minVisible and maxVisible must be in range [0, int.MaxValue]");
        
        // Hard-coded values are from https://ags.cuzk.gov.cz/arcgis1/rest/services/ZTM/ZTM10/MapServer?f=pjson
        var resolutions = new[]
        {
            2048.2600965201932,
            1024.1300482600966,
            512.0650241300483,
            256.03251206502415,
            128.01625603251208,
            64.008128016256038,
            32.004064008128019,
            16.002032004064009,
            8.0010160020320047,
            4.0005080010160023,
            2.0002540005080012,
            1.0001270002540006,
            0.50006350012700029,
            0.25003175006350015,
        };
        
        var schema = new TileSchema
        {
            Name = "ZTM10_EPSG5514",
            Srs = "EPSG:5514",
            Format = "png",
            YAxis = YAxis.OSM,
            Extent = new Extent(-907032.06239999831, -1229928.9814999998, -427819.04259999841, -932316.78950000182),
            OriginX = -925000.0,
            OriginY = -920000.0
        };

        for (var i = 0; i < resolutions.Length; i++)
        {
            schema.Resolutions[i] = new Resolution(i, unitsPerPixel: resolutions[i]);
        }

        IPersistentCache<byte[]>? persistentCache = null;
        ZtmTileCache[ztmDataset.ToString()] = persistentCache;
        var tileSource = new HttpTileSource(
            schema,
            urlFormatter: ZtmBaseRestUrl.Replace("{{ZTM_DATASET}}", ztmDataset.ToString()),
            name: ztmDataset.ToString(),
            persistentCache: persistentCache
        );
        return new TileLayer(tileSource)
        {
            Name = ztmDataset.ToString(),
            MinVisible = minVisible,
            MaxVisible = maxVisible
        };
    }

    public UpdatingPositionLayer CreatePlanesPointLayer(AircraftDatabase aircraftDb, SvgStyleProvider svgStyleProvider, Map map)
    {
        var droneGridIntersectionDetector = new DroneGridIntersectionDetector(DroneGridAssetPath);
        var rpaPresenceDetector = new RpaPresenceConflictDetector(RpaAssetPath);
        var runwayApproachConflictDetector = new RunwayApproachConflictDetector(RunwayStartPointAssetPath, ApproachZoneAssetPath);
        var targetStyleBuilder = new TargetStyleBuilder(svgStyleProvider);
        return new UpdatingPositionLayer(dynamicScoutDataProvider, aircraftDb, targetStyleBuilder, map, droneGridIntersectionDetector, rpaPresenceDetector, runwayApproachConflictDetector, errorDialogService)
        {
            Name = "Position layer",
        };
    }

    public UpdatingTrajectoryLayer CreateTrajectoryLayer(UpdatingPositionLayer positionLayer)
    {
        var style = new SymbolStyle
        {
            Fill = new Brush(Color.FromString("#c3fc05")),
            Outline = new Pen(Color.Black, 2),
            SymbolScale = 0.15f,
            SymbolType = SymbolType.Ellipse
        };

        return new UpdatingTrajectoryLayer(dynamicScoutDataProvider, errorDialogService, positionLayer)
        {
            Name = "Trajectory layer",
            Style = style
        };
    }

    public UpdatingSpeedVectorLayer CreateSpeedVectorLayer(UpdatingPositionLayer positionLayer)
    {
        var style = new VectorStyle
        {
            Line = new Pen
            {
                Color = Color.FromString("#c3fc05"),
                Width = 2
            }
        };

        return new UpdatingSpeedVectorLayer(dynamicScoutDataProvider, errorDialogService, positionLayer)
        {
            Name = "Speed vector layer",
            Style = style
        };
    }

    public IEnumerable<ILayer> CreateAirportElementsLayers(GeoJsonLayerStyleProvider layerStyleProvider, out IReadOnlyList<string> procedureLayerNames)
    {
        var procedureLayerNamesList = new List<string>();
        var layers = layerStyleProvider.GeoJsonLayerProperties.OrderByDescending(layerConfig => layerConfig.Order).Select(layerConfig =>
        {
            var layer = new Layer
            {
                DataSource = layerConfig.Provider,
                Style = layerConfig.Style,
                Opacity = layerConfig.Opacity,
                Name = layerConfig.Name
            };

            if (!layerConfig.Name.StartsWith(ProcedureLayerPrefix))
                return layer;

            procedureLayerNamesList.Add(layerConfig.Name[ProcedureLayerPrefix.Length..]);
            layer.Enabled = false;
            return layer;
        });
        procedureLayerNames = procedureLayerNamesList;
        return layers;
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