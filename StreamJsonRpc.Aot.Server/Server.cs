using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// StreamJsonRpc server-side implementation
public partial class Server : IServer
{
    // unique client identifier per connection
    private Guid clientGuid; 

    // ctlr+c cancel state
    private bool isCancel;

    // heatbeat tick number
    private int tickNumber = 0;

    // JSON-RPC connection to client
    private readonly JsonRpc _jsonRpc;

    // RPC session to client
    public Server(JsonRpc jsonRpc)
    {
        _jsonRpc = jsonRpc;
    }

    // Client connects and registers its Guid
    public async Task<bool> ConnectAsync(Guid guid)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ClientId: {guid}");
        Console.ResetColor();

        this.clientGuid = guid;

        return true;
    }

    //
    // NOTE: Failed to deserialize custom types as JSON-RPC argument
    //
    public Task SetCounterObserver(ICounterObserver observer, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SetNumberStreamListener(INumberStreamListener listner, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}