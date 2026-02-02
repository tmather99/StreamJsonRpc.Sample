using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

// When properly configured, this formatter is safe in Native AOT scenarios for
// the very limited use case shown in this program.
internal static partial class SystemTextJson
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new SystemTextJsonFormatter() {
            JsonSerializerOptions = 
            {
                PropertyNameCaseInsensitive = false,
                IncludeFields = true,
                TypeInfoResolver = JsonTypeInfoResolver.Combine(SourceGenerationContext.Default,
                                                                new DefaultJsonTypeInfoResolver())
            }
        };
    }

    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<Guid, DateTime>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(IObserver<int>))]
    [JsonSerializable(typeof(IObservable<int>))]
    private partial class SourceGenerationContext : JsonSerializerContext;
}
