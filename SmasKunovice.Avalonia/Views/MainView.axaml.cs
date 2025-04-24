using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Mapsui.Tiling;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
    }
}