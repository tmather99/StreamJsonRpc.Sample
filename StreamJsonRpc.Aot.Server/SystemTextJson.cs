using System.Text.Json.Serialization;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

// When properly configured, this formatter is safe in Native AOT scenarios for
// the very limited use case shown in this program.
internal static partial class SystemTextJson
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new SystemTextJsonFormatter() {
            JsonSerializerOptions = {
                TypeInfoResolver = SourceGenerationContext.Default
            },
        };
    }

    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(Dictionary<string, object>))]

    // JSON-RPC protocol messages
    [JsonSerializable(typeof(JsonRpcRequest))]
    [JsonSerializable(typeof(JsonRpcResult))]
    [JsonSerializable(typeof(JsonRpcError))]

    // Streaming payloads
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(ValueTuple<int>))]
    private partial class SourceGenerationContext : JsonSerializerContext;
}