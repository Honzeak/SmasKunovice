using Avalonia.Controls;
using SmasKunovice.Avalonia.ViewModels;

namespace SmasKunovice.Avalonia.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        var vm = new LoginViewModel();
        DataContext = vm;
        vm.LoginSuccessful += Close;
    }
}