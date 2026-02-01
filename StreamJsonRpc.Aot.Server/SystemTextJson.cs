using System.Text.Json.Serialization;
using StreamJsonRpc;

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

    [JsonSerializable(typeof(int))]
    private partial class SourceGenerationContext : JsonSerializerContext;
}
