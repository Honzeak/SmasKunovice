using CommunityToolkit.Mvvm.ComponentModel;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class ErrorWindowViewModel(string errorMessage, string? details) : ViewModelBase
{
    [ObservableProperty]
    private string _errorMessage = errorMessage;
    
    [ObservableProperty]
    private string? _errorDetails = details;
}