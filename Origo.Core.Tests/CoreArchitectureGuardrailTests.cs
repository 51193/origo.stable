using System;
using System.Linq;
using System.Reflection;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class CoreArchitectureGuardrailTests
{
    [Fact]
    public void CoreAssembly_ShouldNotReferenceGodot()
    {
        var refs = typeof(OrigoRuntime).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name != null && r.Name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CorePublicSurface_ShouldNotExposeInternalInfrastructureTypes()
    {
        var exportedNames = typeof(OrigoRuntime).Assembly
            .GetExportedTypes()
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.DoesNotContain("Origo.Core.Abstractions.INodeHost", exportedNames);
        Assert.DoesNotContain("Origo.Core.Snd.SndMappings", exportedNames);
        Assert.DoesNotContain("Origo.Core.Snd.Strategy.SndStrategyPool", exportedNames);
    }

    [Fact]
    public void SndContext_ShouldNotExposeRuntimeOrSystemQueueAsPublicApi()
    {
        var type = typeof(SndContext);
        Assert.Null(type.GetProperty("Runtime", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("FileSystem", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetMethod("EnqueueSystemDeferred", BindingFlags.Instance | BindingFlags.Public));
    }
}
