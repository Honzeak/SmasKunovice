using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _username;
    
    [ObservableProperty]
    private string? _password;
    
    public bool Success { get; private set; }
    public event Action? LoginSuccessful;
    
    [RelayCommand]
    private void Login()
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            return;
        
        Success = true;
        LoginSuccessful?.Invoke();
    }

}