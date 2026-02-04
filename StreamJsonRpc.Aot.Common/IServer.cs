using System.Collections.Generic;
using PolyType;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StreamJsonRpc.Aot.Client")]

namespace StreamJsonRpc.Aot.Common;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IServer
{
    Task<bool> ConnectAsync(Guid guid);
    Task<int> AddAsync(int a, int b);
    Task<List<string>> GetListAsync();
    Task<Dictionary<Guid, DateTime>> GetDictionaryAsync();
    Task<Dictionary<string, string>> GetTableAsync();

    Task SetObserver(IObserver<int> observer, CancellationToken cancellationToken);
    Task<IObserver<int>> GetObserver(CancellationToken cancellationToken);

    Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken cancellationToken);
    Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken cancellationToken);

    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    Task SubscribeToNumberStream();
    Task SubscribeToMouseStream();
}
