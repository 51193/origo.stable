using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.GodotAdapter.FileSystem;

namespace Origo.GodotAdapter.Bootstrap;

public partial class OrigoDefaultEntry
{
    public override void _Ready()
    {
        base._Ready();

        if (AutoDiscoverStrategies)
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(
                Runtime.SndWorld, Runtime.Logger, GodotSkipPrefixes);

        var fileSystem = new GodotFileSystem();
        _sndContext = new SndContext(
            Runtime,
            fileSystem,
            SaveRootPath,
            InitialSaveRootPath,
            ConfigPath);

        SndManager.BindContext(_sndContext);

        ConfigureSaveMetadataContributors(_sndContext);

        Runtime.SndWorld.LoadSceneAliases(fileSystem, SceneAliasMapPath, Runtime.Logger);
        Runtime.SndWorld.LoadTemplates(fileSystem, SndTemplateMapPath, Runtime.Logger);

        _sndContext.RequestLoadMainMenuEntrySave();
        _sndContext.FlushDeferredActionsForCurrentFrame();
    }
}