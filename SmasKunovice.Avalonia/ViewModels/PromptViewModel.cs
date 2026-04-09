using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class PromptViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _inputText;

    // Use a delegate or Action to signal the View to close
    public event Action<string?>? CloseRequested;

    [RelayCommand]
    private void Ok() => CloseRequested?.Invoke(InputText);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(null);
}