using PolyType;

namespace StreamJsonRpc.Jit.Client.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IMouseDataStream : IDataStream
{
}