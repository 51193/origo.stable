using Origo.Core.Runtime;
using Origo.Core.Snd;

namespace Origo.GodotAdapter.Bootstrap;

public partial class OrigoDefaultEntry
{
    public override void _Ready()
    {
        base._Ready();

        if (AutoDiscoverStrategies)
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(
                Runtime.SndWorld, Runtime.Logger, GodotSkipPrefixes);

        _sndContext = new SndContext(
            Runtime,
            SharedFileSystem,
            SaveRootPath,
            InitialSaveRootPath,
            ConfigPath);

        GodotSndBootstrap.BindRuntimeAndContext(SndManager, Runtime.SndWorld, Runtime.Logger, _sndContext);

        ConfigureSaveMetadataContributors(_sndContext);

        Runtime.SndWorld.LoadSceneAliases(SharedFileSystem, SceneAliasMapPath, Runtime.Logger);
        Runtime.SndWorld.LoadTemplates(SharedFileSystem, SndTemplateMapPath, Runtime.Logger);

        _sndContext.RequestLoadMainMenuEntrySave();
        _sndContext.FlushDeferredActionsForCurrentFrame();
    }
}
