using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client;

// Shared code between client and server
public interface IServer
{
    // data type marshaling operations
    Task<bool> ConnectAsync(Guid guid);
    Task<int> AddAsync(int a, int b);
    Task<List<string>> GetListAsync();
    Task<Dictionary<Guid, DateTime>> GetDictionaryAsync();
    Task<Dictionary<string, string>> GetTableAsync();

    // Counter observer operations
    Task SetObserver(IObserver<int> observer, CancellationToken ct);
    Task<IObserver<int>> GetObserver(CancellationToken ct);
    Task SetCounterObserver(IObserver<int> observer, Guid guid, CancellationToken ct);

    // async enumerable operations
    Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken ct);
    Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken ct);
    Task<IAsyncEnumerable<int>> ProcessAsyncEnumerable(IObserver<int> progress, CancellationToken ct);

    // heatbeat start/stop operations
    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    // mouse stream start/stop subscription
    Task SubscribeToNumberStream();
    Task UnsubscribeFromNumberStream();
}