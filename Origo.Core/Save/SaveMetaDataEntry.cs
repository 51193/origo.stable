using System.Collections.Generic;

namespace Origo.Core.Save;

public sealed class SaveMetaDataEntry
{
    public string SaveId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> MetaData { get; init; } = new Dictionary<string, string>();
}
