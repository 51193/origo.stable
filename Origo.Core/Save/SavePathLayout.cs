using System;

namespace Origo.Core.Save;

/// <summary>
///     提供与存档相关的标准相对路径拼装规则。
///     所有返回值均为相对于存档根目录的相对路径，具体根由适配层或系统级黑板决定。
/// </summary>
public static class SavePathLayout
{
    public const string CurrentDirectoryName = "current";

    public static string GetCurrentDirectory()
    {
        return CurrentDirectoryName;
    }

    public static string GetSaveDirectory(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        return $"save_{saveId}";
    }

    public static string GetProgressFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress.json");
    }

    public static string GetProgressStateMachinesFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "progress_state_machines.json");
    }

    public static string GetCustomMetaFile(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));

        return Combine(baseDirectory, "meta.map");
    }

    public static string GetLevelDirectory(string baseDirectory, string levelId)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or whitespace.", nameof(baseDirectory));
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        return Combine(baseDirectory, $"level_{levelId}");
    }

    public static string GetLevelSndSceneFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "snd_scene.json");
    }

    public static string GetLevelSessionFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session.json");
    }

    public static string GetLevelSessionStateMachinesFile(string levelDirectory)
    {
        if (string.IsNullOrWhiteSpace(levelDirectory))
            throw new ArgumentException("Level directory cannot be null or whitespace.", nameof(levelDirectory));

        return Combine(levelDirectory, "session_state_machines.json");
    }

    private static string Combine(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right;
        if (string.IsNullOrEmpty(right))
            return left;
        return $"{left}/{right}";
    }
}