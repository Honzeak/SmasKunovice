using System;
using Avalonia.Controls;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // DataContext = new MainViewViewModel();
        // MapControl.Map = ((MainViewViewModel)DataContext).Map;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewViewModel vm) MapControl.Map = vm.CreateMap();
    }
}