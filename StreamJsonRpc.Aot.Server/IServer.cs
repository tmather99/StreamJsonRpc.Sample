using PolyType;
using StreamJsonRpc;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
internal partial interface IServer
{
    Task<int> AddAsync(int a, int b);
}
