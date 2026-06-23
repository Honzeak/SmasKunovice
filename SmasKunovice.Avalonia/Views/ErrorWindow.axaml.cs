using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class ErrorWindow : Window
{
    public ErrorWindow()
    {
        InitializeComponent();
    }
    
    public ErrorWindow(ErrorWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}