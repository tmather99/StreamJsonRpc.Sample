using PolyType;

namespace StreamJsonRpc.Aot.Common;

// Concrete interface
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ICounterObserver : IObserver<int>
{
}