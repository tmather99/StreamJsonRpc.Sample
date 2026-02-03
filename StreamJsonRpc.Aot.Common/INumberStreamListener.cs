using PolyType;
using StreamJsonRpc;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StreamJsonRpc.Aot.Server")]

namespace StreamJsonRpc.Aot.Common;

// Shared interface between client and server
public partial interface IListener
{
    Task OnNextValue(int value);
    Task OnError(string error);
    Task OnCompleted();
}

// Concreate interface
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberStreamListener : IListener
{
}
