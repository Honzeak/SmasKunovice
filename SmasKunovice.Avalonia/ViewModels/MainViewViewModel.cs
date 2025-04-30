using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Extensions.Provider;
using Mapsui.Layers;
using Mapsui.Nts.Providers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainViewViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}