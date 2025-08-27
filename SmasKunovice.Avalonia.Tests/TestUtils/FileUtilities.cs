namespace SmasKunovice.Avalonia.Tests.TestUtils;

public static class FileUtilities
{
    public static Dictionary<string, string> GetTestFiles(string testClassName)
    {
        return Directory.EnumerateFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData"),
                $"{testClassName}*.*")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path).Replace(testClassName, string.Empty),
                path => path);
    }

    public static string GetTestFile(string testClassName, string fileNameWithoutExtension)
    {
        return Directory
            .EnumerateFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData"),
                $"{testClassName}*.*").Single(path =>
                Path.GetFileNameWithoutExtension(path).Replace(testClassName, string.Empty).Equals(fileNameWithoutExtension));
    }
}