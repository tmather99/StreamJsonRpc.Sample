using System.Text.Json.Serialization;
using StreamJsonRpc;

namespace StreamJsonRpc.Aot.Common;

// When properly configured, this formatter is safe in Native AOT scenarios for
// the very limited use case shown in this program.
public static partial class SystemTextJson
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new SystemTextJsonFormatter() {
            JsonSerializerOptions = {
                TypeInfoResolver = SourceGenerationContext.Default
            },
        };
    }

    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<Guid, DateTime>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<int, MouseAction>))]
    [JsonSerializable(typeof(Dictionary<int, MouseEventData>))]
    private partial class SourceGenerationContext : JsonSerializerContext;
}