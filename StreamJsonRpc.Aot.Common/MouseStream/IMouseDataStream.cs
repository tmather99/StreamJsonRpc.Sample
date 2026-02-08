using PolyType;

namespace StreamJsonRpc.Aot.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IMouseDataStream
{
    Task Subscribe(Guid clientGuid);
    Task Unsubscribe(Guid clientGuid);
}