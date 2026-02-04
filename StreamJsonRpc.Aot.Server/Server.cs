using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation
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

    // Client requests to cancel tick operation
    public Task CancelTickOperation(Guid guid)
    {
        isCancel = true;
        Console.WriteLine($"Cancel Tick Operation for {guid}");
        return Task.CompletedTask;
    }

    // Server to start sending periodic tick notifications to client
    public async Task SendTicksAsync(Guid guid)
    {
        Console.WriteLine($"  SendTicksAsync isCancel={isCancel}");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        while (!isCancel)
        {
            // Send notification with tick number sequence per client
            await _jsonRpc.NotifyAsync("Tick", ++tickNumber);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Notify clientId {guid} - #{tickNumber}");
            Console.ResetColor();
            await Task.Delay(1000);
        }

        _randomNumberGenerator.Dispose();
        _numberSubscription?.Dispose();
        _numberSubscription = null!;
    }
}