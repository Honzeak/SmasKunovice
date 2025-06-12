using SmasKunovice.Avalonia.Models.FakeClient;

namespace SmasKunovice.Avalonia.Tests;

public class LogfileDronetagClientTests
{
    
[Test]
public void Constructor_WithValidJsonLogFile_InitializesSuccessfully()
{
    // Arrange
    var jsonLogFile = Utilities.GetTestFile(nameof(LogfileDronetagClientTests), "_log");

    // Act
    var client = new LogfileDronetagClient(jsonLogFile, 1000);

    // Assert
    Assert.Pass();

    // Note: No cleanup needed as we're using an existing file
}
}