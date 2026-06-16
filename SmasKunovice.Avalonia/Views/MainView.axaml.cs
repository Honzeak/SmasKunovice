using System;
using Avalonia.Controls;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewViewModel viewModel)
            return;
        
        MapControl.Map = viewModel.Map;
    }
}