using PolyType;
using StreamJsonRpc;

internal static partial class NerdbankMessagePack
{
    public static IJsonRpcMessageFormatter CreateFormatter()
    {
        return new NerdbankMessagePackFormatter() {
            TypeShapeProvider = Witness.GeneratedTypeShapeProvider,
        };
    }

    [GenerateShapeFor<int>]
    private partial class Witness;
}
