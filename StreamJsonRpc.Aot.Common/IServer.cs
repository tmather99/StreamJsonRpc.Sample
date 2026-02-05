using System.Collections.Generic;
using PolyType;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("StreamJsonRpc.Aot.Client")]

namespace StreamJsonRpc.Aot.Common;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IServer
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
    Task SetCounterObserver(IObserver<int> observer, Guid oid, CancellationToken ct);

    //
    // NOTE: Failed to deserialize custom types as JSON-RPC argument
    //
    Task SetCounterObserver(ICounterObserver observer, CancellationToken ct);
    Task SetNumberStreamListener(INumberStreamListener listner, CancellationToken ct);

    // async enumerable operations
    Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken ct);
    Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken ct);
    Task<IAsyncEnumerable<int>> ProcessAsyncEnumerable(IObserver<int> progress, CancellationToken ct);
    Task<IAsyncEnumerable<int>> DuplexAsyncEnumerable(IAsyncEnumerable<int> tag, CancellationToken ct);

    // heatbeat start/stop operations
    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    // mouse and number stream subscriptions
    Task SubscribeToNumberStream();
    Task SubscribeToMouseStream();
}
