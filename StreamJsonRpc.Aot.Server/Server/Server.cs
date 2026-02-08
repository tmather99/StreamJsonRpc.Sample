using System.IO.Pipes;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// StreamJsonRpc server-side implementation
public partial class Server : IServer
{
    // unique client identifier per connection
    private Guid clientGuid;

    // ctlr+c cancel state
    public bool isCancel;

    // heatbeat tick number
    private int tickNumber = 0;

    // JSON-RPC connection to client
    private readonly JsonRpc _jsonRpc;

    // RPC session to client
    public Server(JsonRpc jsonRpc)
    {
        _jsonRpc = jsonRpc;
    }

    // Handle each client connection
    public static async Task RunAsync(NamedPipeServerStream pipe, int requestId)
    {
        await Console.Error.WriteLineAsync($"  Connection request #{requestId} received.");

        // Set up JSON-RPC over the named pipe
        JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe)) {
            CancelLocallyInvokedMethodsWhenConnectionIsClosed = true
        };

        jsonRpc.Disconnected += static async delegate (object? o, JsonRpcDisconnectedEventArgs e) {
            Console.WriteLine("\nRPC connection closed");
            Console.WriteLine($"  Reason: {e.Reason}");
            Console.WriteLine($"  Description: {e.Description}");
            if (e.Exception != null)
                Console.WriteLine($"  Exception: {e.Exception}");
        };

        RegisterTypes(jsonRpc);

        jsonRpc.StartListening();

        //await jsonRpc.Completion;
        await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");
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

    // Do not use reflection. Everything must be known at compile time.
    static void RegisterTypes(JsonRpc jsonRpc)
    {
        // Register IServer
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
        IServer server = new Server(jsonRpc);
        jsonRpc.AddLocalRpcTarget(targetMetadata, server, null);

        // Register IUserService
        targetMetadata = RpcTargetMetadata.FromShape<IUserService>();
        IUserService userService = new UserService(jsonRpc);
        jsonRpc.AddLocalRpcTarget(targetMetadata, userService, null);

        // Register IMouseDataStream
        targetMetadata = RpcTargetMetadata.FromShape<IMouseDataStream>();
        IMouseDataStream mouseDataStream = new MouseDataStream(jsonRpc);
        jsonRpc.AddLocalRpcTarget(targetMetadata, mouseDataStream, null);

        // Register INumberDataStream
        targetMetadata = RpcTargetMetadata.FromShape<INumberDataStream>();
        INumberDataStream numberDataStream = new NumberDataStream(jsonRpc);
        jsonRpc.AddLocalRpcTarget(targetMetadata, numberDataStream, null);
    }
}