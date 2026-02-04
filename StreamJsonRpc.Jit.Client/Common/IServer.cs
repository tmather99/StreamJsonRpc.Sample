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
    Task SetObserver(IObserver<int> observer, CancellationToken cancellationToken);
    Task<IObserver<int>> GetObserver(CancellationToken cancellationToken);

    Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken cancellationToken);
    Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken cancellationToken);

    // heatbeat start/stop operations
    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    // mouse and number stream subscriptions
    Task SubscribeToNumberStream();
    Task SubscribeToMouseStream();
}