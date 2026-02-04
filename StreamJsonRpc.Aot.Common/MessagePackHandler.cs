using System.IO.Pipes;
using System.Text.Json.Serialization;
using Microsoft;
using PolyType;

namespace StreamJsonRpc.Aot.Common;

public static class MessagePackHandler
{
    public static IJsonRpcMessageHandler Create(PipeStream pipe, string formatter = "NerdbankMessagePack")
    {
        return formatter switch {
            "JSON" => new HeaderDelimitedMessageHandler(pipe, SystemTextJson.CreateFormatter()),
            "MessagePack" => new LengthHeaderMessageHandler(pipe, pipe, new MessagePackFormatter()),
            "NerdbankMessagePack" => new LengthHeaderMessageHandler(pipe, pipe, NerdbankMessagePack.CreateFormatter()),
            _ => throw Assumes.NotReachable(),
        };
    }
}

public static partial class NerdbankMessagePack
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new NerdbankMessagePackFormatter() 
        {
            TypeShapeProvider = Witness.GeneratedTypeShapeProvider,
        };
    }

    [GenerateShapeFor<string>]
    [GenerateShapeFor<bool>]
    [GenerateShapeFor<int>]
    [GenerateShapeFor<long>]
    [GenerateShapeFor<double>]
    [GenerateShapeFor<Guid>]
    [GenerateShapeFor<List<string>>]
    [GenerateShapeFor<Dictionary<Guid, DateTime>>]
    [GenerateShapeFor<Dictionary<string, string>>]
    [GenerateShapeFor<Dictionary<int, MouseAction>>]
    [GenerateShapeFor<Dictionary<int, MouseEventData>>]
    public partial class Witness;
}

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
