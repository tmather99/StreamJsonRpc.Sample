using PolyType;

namespace StreamJsonRpc.Aot.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IMouseStreamListener : IStreamListener<MouseEventData>
{
}