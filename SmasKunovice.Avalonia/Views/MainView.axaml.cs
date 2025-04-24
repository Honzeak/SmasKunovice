using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Mapsui.Tiling;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
    }
    
    public static class GeoTiffFileProvider
    {
        public const string GeoTiffPath = @"C:\Users\honza\OneDrive\Code\SMAS-Data\Kunovice_tiff\";

        public static IEnumerable<string> GetAllGeoTiffFiles()
        {
            return Directory.EnumerateFiles(GeoTiffPath, "*.tif", SearchOption.TopDirectoryOnly);
        }
        
    }
}