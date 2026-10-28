using System;
using System.Diagnostics;
using System.Reflection;

namespace SmasKunovice.Avalonia;

public static class AppVersionInfo
{
    private static readonly Lazy<string> CachedVersion = new(() =>
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        // Prefers AssemblyInformationalVersion (e.g., "1.2.3-build.45+commit.a1b2c3d")
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            return infoVersion;
        }

        // Fallback to FileVersion or Assembly version
        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(location);
            if (!string.IsNullOrWhiteSpace(fileVersionInfo.FileVersion))
            {
                return fileVersionInfo.FileVersion;
            }
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    });

    public static string DisplayVersion => CachedVersion.Value;
}
