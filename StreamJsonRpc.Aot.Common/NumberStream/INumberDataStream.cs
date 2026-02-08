using PolyType;

namespace StreamJsonRpc.Aot.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberDataStream : IDataStream
{
    [JsonRpcMethod("INumberDataStream.Subscribe")]
    new Task Subscribe(Guid clientGuid);

    [JsonRpcMethod("INumberDataStream.Unsubscribe")]
    new Task Unsubscribe(Guid clientGuid);
}