using System.IO.Pipes;
using Microsoft;
using PolyType.ReflectionProvider;

namespace StreamJsonRpc.Jit.Client;

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

public static class NerdbankMessagePack
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new NerdbankMessagePackFormatter() {
            TypeShapeProvider = ReflectionTypeShapeProvider.Default
        };
    }
}
