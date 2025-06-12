using CommunityToolkit.Mvvm.ComponentModel;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class MainWindowViewModel(MainViewViewModel mainViewViewModel) : ViewModelBase
{
    [ObservableProperty] private MainViewViewModel _mainViewViewModel = mainViewViewModel;
}