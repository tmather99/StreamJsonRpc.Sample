using PolyType;
using StreamJsonRpc;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StreamJsonRpc.Aot.Server")]

namespace StreamJsonRpc.Aot.Common;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberStreamListener
{
    Task OnNextValue(int value);
    Task OnError(string error);
    Task OnCompleted();
}