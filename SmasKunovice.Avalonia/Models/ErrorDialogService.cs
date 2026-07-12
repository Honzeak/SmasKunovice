using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SmasKunovice.Avalonia.ViewModels;
using SmasKunovice.Avalonia.Views;

namespace SmasKunovice.Avalonia.Models;

public class ErrorDialogService : IErrorDialogService
{
    public async ValueTask ShowErrorDialogAsync(string errorMessage, Exception? exception = null)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = new ErrorWindow(new ErrorWindowViewModel(errorMessage, exception?.Message));
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parent = desktop?.MainWindow;
            if (parent is not null)
            {
                await window.ShowDialog(parent);
            }
            else
            {
                var taskCompletionSource = new TaskCompletionSource();
                window.Closed += (s, e) => taskCompletionSource.SetResult();
                window.Show();
                await taskCompletionSource.Task;
            }
        });
    }
}

public interface IErrorDialogService
{
    ValueTask ShowErrorDialogAsync(string errorMessage, Exception? exception = null);
}