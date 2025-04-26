using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Mapsui;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Color = Mapsui.Styles.Color;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        CreateMap();
        MapControl.Map = CreateMap();
        MapControl.Map.Refresh();
    }

    private Map CreateMap()
    {
        var layers = GeoTiffFileProvider.GetAllGeoTiffFiles().Select(ILayer (x) =>
        {
            var provider = new GeoTiffProvider(x.filePath);
            var layer = new Layer
            {
                DataSource = provider,
                Name = x.fileName,
                Style = new RasterStyle(),
            };
            return layer;
        }).ToArray();
        var map = new Map();
        map.Layers.Add(layers);
        return map;
    }

    public static class GeoTiffFileProvider
    {
        public const string GeoTiffPath = @"C:\Users\honza\OneDrive\Code\SMAS-Data\Kunovice_tiff\";

        public static IEnumerable<(string fileName, string filePath)> GetAllGeoTiffFiles()
        {
            return Directory.EnumerateFiles(GeoTiffPath, "*.tif", SearchOption.TopDirectoryOnly)
                .Select(path => (Path.GetFileName(path), path));
        }
        
    }

}