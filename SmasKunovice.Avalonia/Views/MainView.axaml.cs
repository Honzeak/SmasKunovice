using Avalonia.Controls;
using Avalonia.Logging;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewViewModel();
        MapControl.Map = ((MainViewViewModel)DataContext).Map;
        
    }

}