using Origo.Core.Abstractions;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     挂载在 SND 实体上的策略基类，提供实体生命周期钩子。
/// </summary>
public abstract class EntityStrategyBase : BaseStrategy
{
    public virtual void Process(ISndEntity entity, double delta, SndContext ctx)
    {
    }

    public virtual void AfterSpawn(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void AfterLoad(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void AfterAdd(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void BeforeRemove(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void BeforeSave(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void BeforeQuit(ISndEntity entity, SndContext ctx)
    {
    }

    public virtual void BeforeDead(ISndEntity entity, SndContext ctx)
    {
    }
}
