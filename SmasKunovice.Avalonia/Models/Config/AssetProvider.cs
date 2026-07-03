using System;
using System.IO;

namespace SmasKunovice.Avalonia.Models;

public static class AssetProvider
{
    public static string GetFullAssetPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);
    }
}