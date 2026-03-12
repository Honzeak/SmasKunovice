using CommunityToolkit.Mvvm.ComponentModel;

namespace SmasKunovice.Avalonia.ViewModels;

public partial class SelectProcedure(string name) : ObservableObject
{
    [ObservableProperty] private bool _isChecked = false;
    public string Name { get; init; } = name;
}