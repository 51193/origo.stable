using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Save;

internal sealed class BlackboardJsonSerializer
{
    private readonly JsonSerializerOptions _options;

    public BlackboardJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public string Serialize(IBlackboard blackboard)
    {
        var data = blackboard.ExportAll();
        return JsonSerializer.Serialize(data, _options);
    }

    public void DeserializeInto(IBlackboard blackboard, string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, TypedData>>(json, _options)
                   ?? new Dictionary<string, TypedData>();
        blackboard.ImportAll(dict);
    }
}