using Avalonia.Controls;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class PromptWindow : Window
{
    public PromptWindow()
    {
        InitializeComponent();
        var vm = new PromptViewModel();
        DataContext = vm;
        vm.CloseRequested += Close;
    }
}