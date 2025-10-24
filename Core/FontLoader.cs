using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;

namespace CS2GameHelper.Core;

public static class FontLoader
{
    private static readonly Dictionary<string, PrivateFontCollection> FontCollections =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> FontFamilies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncRoot = new();

    public static string EnsureFont(string relativePath)
    {
        lock (SyncRoot)
        {
            if (FontFamilies.TryGetValue(relativePath, out var familyName)) return familyName;

            var absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"Font file not found at '{absolutePath}'", absolutePath);

            var collection = new PrivateFontCollection();
            collection.AddFontFile(absolutePath);
            var fontFamily = collection.Families.FirstOrDefault()?.Name;
            if (string.IsNullOrEmpty(fontFamily))
                throw new InvalidOperationException($"Unable to read font family for '{absolutePath}'.");

            FontCollections[relativePath] = collection;
            FontFamilies[relativePath] = fontFamily;
            return fontFamily;
        }
    }
}
