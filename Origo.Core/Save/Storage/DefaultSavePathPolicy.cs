namespace Origo.Core.Save.Storage;

/// <summary>
///     <see cref="ISavePathPolicy" /> 的默认实现，委托给 <see cref="SavePathLayout" /> 静态方法。
/// </summary>
internal sealed class DefaultSavePathPolicy : ISavePathPolicy
{
    public string GetCurrentDirectory()
    {
        return SavePathLayout.GetCurrentDirectory();
    }

    public string GetSaveDirectory(string saveId)
    {
        return SavePathLayout.GetSaveDirectory(saveId);
    }

    public string GetProgressFile(string baseDirectory)
    {
        return SavePathLayout.GetProgressFile(baseDirectory);
    }

    public string GetProgressStateMachinesFile(string baseDirectory)
    {
        return SavePathLayout.GetProgressStateMachinesFile(baseDirectory);
    }

    public string GetCustomMetaFile(string baseDirectory)
    {
        return SavePathLayout.GetCustomMetaFile(baseDirectory);
    }

    public string GetLevelDirectory(string baseDirectory, string levelId)
    {
        return SavePathLayout.GetLevelDirectory(baseDirectory, levelId);
    }

    public string GetLevelSndSceneFile(string levelDirectory)
    {
        return SavePathLayout.GetLevelSndSceneFile(levelDirectory);
    }

    public string GetLevelSessionFile(string levelDirectory)
    {
        return SavePathLayout.GetLevelSessionFile(levelDirectory);
    }

    public string GetLevelSessionStateMachinesFile(string levelDirectory)
    {
        return SavePathLayout.GetLevelSessionStateMachinesFile(levelDirectory);
    }

    public string GetWriteInProgressMarker(string baseDirectory)
    {
        return SavePathLayout.GetWriteInProgressMarker(baseDirectory);
    }
}