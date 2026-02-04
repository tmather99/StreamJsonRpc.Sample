using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client;

// Shared code between client and server
public interface IServer
{
    Task<bool> ConnectAsync(Guid guid);
    Task<int> AddAsync(int a, int b);
    Task<List<string>> GetListAsync();
    Task<Dictionary<Guid, DateTime>> GetDictionaryAsync();
    Task<Dictionary<string, string>> GetTableAsync();

    Task SetObserver(IObserver<int> observer);
    Task<IObserver<int>> GetObserver();

    Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken cancellationToken);
    Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken cancellationToken);

    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    Task SubscribeToNumberStream();
    Task SubscribeToMouseStream();
}