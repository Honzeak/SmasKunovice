using SmasKunovice.Avalonia.Models.FakeClient;
using SmasKunovice.Avalonia.Tests.TestUtils;

namespace SmasKunovice.Avalonia.Tests;

public class LogfileDronetagClientTests : TestBase
{
    
[Test]
[Ignore("Obsolete log with incorrect format")]
// TODO get valid data to enable this test
public void Constructor_WithValidJsonLogFile_InitializesSuccessfully()
{
    // Arrange
    var jsonLogFile = FileUtilities.GetTestFile(nameof(LogfileDronetagClientTests), "_log");

    // Act
    var client = new LogfileDronetagClient(jsonLogFile, 1000);

    // Assert
    Assert.Pass();

    // Note: No cleanup needed as we're using an existing file
}
}