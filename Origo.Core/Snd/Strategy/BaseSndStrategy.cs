using Origo.Core.Abstractions;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     SND 策略的抽象基类。
///     通过 SndContext 访问运行时、黑板与存档能力。
///     <para>
///         <b>
///             重要：策略实例通过 SndStrategyPool 在多个实体之间共享复用。
///             子类必须保持无状态——禁止声明实例字段或属性来存储运行时数据。
///             所有可变状态必须存储在实体的 Data 中（通过 ISndEntity.SetData/GetData 访问）。
///             自动发现阶段会校验策略类型，若存在实例字段将拒绝注册并记录错误日志。
///         </b>
///     </para>
/// </summary>
public abstract class BaseSndStrategy
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