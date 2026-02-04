using PolyType;

namespace StreamJsonRpc.Aot.Common;

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