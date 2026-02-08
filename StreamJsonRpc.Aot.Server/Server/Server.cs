using System.IO.Pipes;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// StreamJsonRpc server-side implementation
public partial class Server : IServer, IDisposable
{
    // unique client identifier per connection
    public Guid clientGuid;

    // ctlr+c cancel state
    public bool isCancel;

    // heatbeat tick number
    private int tickNumber = 0;

    // JSON-RPC connection to client
    public readonly JsonRpc jsonRpc;

    // Per-connection services
    private readonly IUserService _userService;
    private readonly IMouseDataStream _mouseDataStream;
    private readonly INumberDataStream _numberDataStream;

    // RPC session to client
    public Server(JsonRpc jsonRpc)
    {
        this.jsonRpc = jsonRpc;

        // create per-connection services here
        _userService = new UserService(jsonRpc);
        _mouseDataStream = new MouseDataStream(this);
        _numberDataStream = new NumberDataStream(this);

        jsonRpc.Disconnected += OnRpcDisconnected;
    }

    public void Dispose()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        // Here you can:
        // - dispose subscriptions
        // - cancel timers
        // - close streams
        // - clear clientGuid, etc.

        isCancel = true;

        (_mouseDataStream as IDisposable)?.Dispose();
        (_numberDataStream as IDisposable)?.Dispose();
        (_userService as IDisposable)?.Dispose();
    }

    // Cleanup when client disconnects or cancels the request.
    private async void OnRpcDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        Console.WriteLine("\nServer - RPC connection closed");

        // This is the client associated with this JsonRpc/Server instance
        Console.WriteLine($"     ClientId: {clientGuid}");
        Console.WriteLine($"       Reason: {e.Reason}");
        Console.WriteLine($"  Description: {e.Description}");
        if (e.Exception != null)
            Console.WriteLine($"  Exception: {e.Exception}");

        // Do any per-client cleanup here
        Cleanup();

        // In case we need to wait on async cleanup
        await Task.CompletedTask;
    }

    // Handle each client connection
    public static async Task<JsonRpc> RunAsync(NamedPipeServerStream pipe, int requestId)
    {
        await Console.Error.WriteLineAsync($"  Connection request #{requestId} received.");

        // Set up JSON-RPC over the named pipe
        JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe)) {
            CancelLocallyInvokedMethodsWhenConnectionIsClosed = true
        };

        var server = new Server(jsonRpc);
        RegisterTypes(jsonRpc, server);

        jsonRpc.StartListening();

        await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");

        return jsonRpc;
    }

    // Client connects and registers its Guid
    public async Task<bool> ConnectAsync(Guid clientGui)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ClientId: {clientGui}");
        Console.ResetColor();

        this.clientGuid = clientGui;

        return true;
    }

    // Do not use reflection. Everything must be known at compile time.
    static void RegisterTypes(JsonRpc jsonRpc, Server server)
    {
        // Register IServer
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
        jsonRpc.AddLocalRpcTarget(targetMetadata, server, null);

        // Register IUserService
        targetMetadata = RpcTargetMetadata.FromShape<IUserService>();
        jsonRpc.AddLocalRpcTarget(targetMetadata, server._userService, null);

        // Register IMouseDataStream
        targetMetadata = RpcTargetMetadata.FromShape<IMouseDataStream>();
        jsonRpc.AddLocalRpcTarget(targetMetadata, server._mouseDataStream, null);

        // Register INumberDataStream
        targetMetadata = RpcTargetMetadata.FromShape<INumberDataStream>();
        jsonRpc.AddLocalRpcTarget(targetMetadata, server._numberDataStream, null);
    }
}