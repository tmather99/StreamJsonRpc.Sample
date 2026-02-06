using System.IO.Pipes;
using Microsoft;
using PolyType;
using StreamJsonRpc.Aot.Common.MouseStream;

namespace StreamJsonRpc.Aot.Common;

public static class MessagePackHandler
{
    public static IJsonRpcMessageHandler Create(PipeStream pipe, string formatter = "NerdbankMessagePack")
    {
        return formatter switch {
            "JSON" => new HeaderDelimitedMessageHandler(pipe, new JsonMessageFormatter()),
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
        return new NerdbankMessagePackFormatter() {
            TypeShapeProvider = Witness.GeneratedTypeShapeProvider,
        };
    }

    [GenerateShapeFor<Dictionary<int, MouseAction>>]
    [GenerateShapeFor<Dictionary<int, MouseEventData>>]
    public partial class Witness;
}