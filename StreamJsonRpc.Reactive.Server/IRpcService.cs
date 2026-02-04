using PolyType;
using StreamJsonRpc;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
internal partial interface IRpcService : IDisposable
{
    Task<bool> GetBool();

    Task<IAsyncEnumerable<int>> GetAsyncEnumerable();

    Task Subcribe(IObserver<int> observer);

    Task<IObserver<int>> GetObserver();
}
