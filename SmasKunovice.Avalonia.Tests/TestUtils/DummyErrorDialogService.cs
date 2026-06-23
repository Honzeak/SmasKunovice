using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Avalonia.Tests.TestUtils;

public class DummyErrorDialogService : IErrorDialogService
{
    public ValueTask ShowErrorDialogAsync(string errorMessage, Exception? exception = null)
    {
        return ValueTask.CompletedTask;
    }
}