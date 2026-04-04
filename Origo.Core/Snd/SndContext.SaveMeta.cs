using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;

namespace Origo.Core.Snd;

public sealed partial class SndContext
{
    /// <summary>
    ///     注册展示用 <c>meta.map</c> 贡献者；同一 <see cref="SndContext" /> 上可多次注册，按顺序执行，同名键后者覆盖前者；
    ///     存档时传入的 <c>customMeta</c> 在全部贡献者之后再次键级覆盖。
    /// </summary>
    public void RegisterSaveMetaContributor(ISaveMetaContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        _saveMetaContributors.Add(contributor);
    }

    /// <summary>
    ///     使用委托注册展示用 meta 贡献者，语义同 <see cref="RegisterSaveMetaContributor(ISaveMetaContributor)" />。
    /// </summary>
    public void RegisterSaveMetaContributor(Action<SaveMetaBuildContext, IDictionary<string, string>> contribute)
    {
        ArgumentNullException.ThrowIfNull(contribute);
        _saveMetaContributors.Add(new DelegateSaveMetaContributor(contribute));
    }
}
