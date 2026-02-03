using System.IO.Pipes;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Server;

string pipeName = "Satori";
int clientRequests = 0;

while (true)
{
    await Console.Error.WriteLineAsync($"\nWaiting for client to make on {pipeName}...\n");

    NamedPipeServerStream stream = new(pipeName,
                                       PipeDirection.InOut,
                                       NamedPipeServerStream.MaxAllowedServerInstances,
                                       PipeTransmissionMode.Byte,
                                       PipeOptions.Asynchronous);

    await stream.WaitForConnectionAsync();
    await RunAsync(stream, ++clientRequests);
}

static async Task RunAsync(NamedPipeServerStream pipe, int requestId)
{
    await Console.Error.WriteLineAsync($"  Connection request #{requestId} received.");

    HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
    JsonRpc jsonRpc = new(messageHandler);
    jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;

    jsonRpc.Disconnected += static async delegate (object? o, JsonRpcDisconnectedEventArgs e)
    {
        Console.WriteLine("\nRPC connection closed");
        Console.WriteLine($"  Reason: {e.Reason}");
        Console.WriteLine($"  Description: {e.Description}");
        if (e.Exception != null)
            Console.WriteLine($"  Exception: {e.Exception}");
    };

    RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
    Server server = new Server();
    jsonRpc.AddLocalRpcTarget(targetMetadata, server, null);

    // Give server reference to JSON-RPC so it can call back to client
    server.SetClientRpc(jsonRpc);

    jsonRpc.StartListening();

    //await jsonRpc.Completion;
    await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");
}
    