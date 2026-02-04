using System.IO.Pipes;
using StreamJsonRpc;
using StreamJsonRpc.Reactive.Server;

string pipeName = "rpcpipe";
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

    JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe));

    RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IRpcService>();
    jsonRpc.AddLocalRpcTarget(targetMetadata, new RpcService(), null);
    jsonRpc.StartListening();

    await jsonRpc.Completion;
    await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");
}
