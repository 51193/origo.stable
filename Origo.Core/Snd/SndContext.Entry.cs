using System;
using Origo.Core.Runtime;
using Origo.Core.Save;

namespace Origo.Core.Snd;

public sealed partial class SndContext
{
    public void RequestLoadInitialSave()
    {
        EnqueueSystemDeferred(ExecuteLoadInitialSaveNow);
    }

    /// <summary>
    ///     按启动流程重新读取主菜单 entry 配置（与 OrigoDefaultEntry.ConfigPath 一致）。
    ///     不包含隐式保存；若业务需要保存，请先显式调用 RequestSaveGame/RequestSaveGameAuto。
    /// </summary>
    public void RequestLoadMainMenuEntrySave()
    {
        EnqueueSystemDeferred(ExecuteLoadMainMenuEntrySaveNow);
    }

    private void ExecuteLoadInitialSaveNow()
    {
        Runtime.ResetConsoleState();

        ShutdownCurrentProgressAndScene();

        var payload = SaveStorageFacade.ReadSavePayloadFromSnapshot(
            FileSystem,
            InitialSaveRootPath,
            DefaultInitialSaveId,
            DefaultInitialLevelId);

        payload.SaveId = DefaultInitialSaveId;

        SaveStorageFacade.WriteSavePayloadToCurrent(FileSystem, SaveRootPath, payload);
        var progressRun = _runFactory.CreateProgressRun(
            DefaultInitialSaveId,
            payload.ActiveLevelId,
            new Blackboard.Blackboard());
        // 必须先绑定 ProgressRun，再加载 payload（实体 AfterLoad 期间会访问 session state machines）。
        _progressRun = progressRun;
        progressRun.LoadFromPayload(payload);
        ClearContinueTarget();
    }

    private void ExecuteLoadMainMenuEntrySaveNow()
    {
        Runtime.ResetConsoleState();

        ShutdownCurrentProgressAndScene();

        var saveId = TryGetActiveSaveId() ?? DefaultInitialSaveId;
        var mainMenuProgressRun = _runFactory.CreateProgressRun(
            saveId,
            DefaultMainMenuLevelId,
            new Blackboard.Blackboard());
        mainMenuProgressRun.CreateFromAlreadyLoadedScene();
        _progressRun = mainMenuProgressRun;

        // 必须先创建 Progress/Session 级状态机容器，再 Spawn 实体，
        // 以保证实体策略（例如主摄像机入栈）能正确访问 session state machines。
        OrigoAutoInitializer.LoadAndSpawnFromFile(
            EntryConfigPath,
            Runtime.Snd,
            FileSystem,
            Runtime.Logger);
    }

    /// <summary>
    ///     切换到新关卡（通过 ProgressRun 执行标准切换序列）。
    /// </summary>
    public void RequestChangeLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));

        EnqueueBusinessDeferred(() => EnsureProgressRun().SwitchLevel(newLevelId));
    }
}