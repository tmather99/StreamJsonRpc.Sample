using PolyType;
using StreamJsonRpc;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StreamJsonRpc.Aot.Server")]

namespace StreamJsonRpc.Aot.Common;

// Concrete interface
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberStreamStreamListener : IStreamListener<int>
{
}
