namespace Origo.GodotAdapter.FileSystem;

internal static class GodotPathHelper
{
    public static string Combine(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return relativePath;
        if (string.IsNullOrEmpty(relativePath))
            return basePath;

        return $"{basePath.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    public static string GetParentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0)
            return string.Empty;

        return trimmed[..lastSlash];
    }
}