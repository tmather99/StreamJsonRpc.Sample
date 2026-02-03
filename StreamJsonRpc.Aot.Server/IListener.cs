using PolyType;
using StreamJsonRpc;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
internal partial interface IListener
{
    Task OnNextValue(int value);
    Task OnError(string error);
    Task OnCompleted();
}