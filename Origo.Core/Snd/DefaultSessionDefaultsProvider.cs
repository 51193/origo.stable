namespace Origo.Core.Snd;

/// <summary>
///     <see cref="ISessionDefaultsProvider" /> 的默认实现，返回 <see cref="SndDefaults" /> 中的常量。
/// </summary>
public sealed class DefaultSessionDefaultsProvider : ISessionDefaultsProvider
{
    public string InitialSaveId => SndDefaults.InitialSaveId;

    public string InitialLevelId => SndDefaults.InitialLevelId;

    public string MainMenuLevelId => SndDefaults.MainMenuLevelId;
}
