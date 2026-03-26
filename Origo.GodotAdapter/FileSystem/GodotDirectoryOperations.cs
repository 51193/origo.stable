using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Origo.GodotAdapter.FileSystem;

internal static class GodotDirectoryOperations
{
    public static bool Exists(string path)
    {
        return DirAccess.DirExistsAbsolute(path);
    }

    public static void Create(string directoryPath)
    {
        DirAccess.MakeDirRecursiveAbsolute(directoryPath);
    }

    public static IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        using var dir = DirAccess.Open(directoryPath);
        if (dir == null)
            return [];

        var normalizedDir = directoryPath.TrimEnd('/');
        IEnumerable<string> fileNames = dir.GetFiles();

        if (!string.IsNullOrEmpty(searchPattern) && searchPattern.StartsWith('*'))
        {
            var suffix = searchPattern[1..];
            fileNames = fileNames.Where(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        var result = fileNames.Select(f => $"{normalizedDir}/{f}").ToList();

        if (recursive)
            foreach (var subdir in dir.GetDirectories())
                result.AddRange(EnumerateFiles($"{normalizedDir}/{subdir}", searchPattern, true));

        return result;
    }

    public static IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        using var dir = DirAccess.Open(directoryPath);
        if (dir == null)
            return [];

        var normalizedDir = directoryPath.TrimEnd('/');
        return dir.GetDirectories().Select(d => $"{normalizedDir}/{d}").ToArray();
    }
}