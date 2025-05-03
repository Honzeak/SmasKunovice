using System.Text.Json;
using SmasKunovice.Avalonia.Models;

namespace SmasKunovice.Tests;

public class ScoutDataTests
{
    private Dictionary<string, string> _testFiles;

    [SetUp]
    public void Setup()
    {
        _testFiles = Directory.EnumerateFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData"), "*.json")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path).Replace(nameof(ScoutDataTests), string.Empty), path => path);
    }

    [Test]
    public void DeserializeTest()
    {
        var jsonStream = File.OpenRead(_testFiles["_single_1"]);
        var obj = JsonSerializer.Deserialize<ScoutData>(jsonStream, ScoutData.SerializerOptions);
        Assert.That(obj, Is.Not.Null);
    }
}