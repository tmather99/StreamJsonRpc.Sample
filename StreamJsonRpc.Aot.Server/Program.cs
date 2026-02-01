using System.IO.Pipes;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Server;

int clientRequests = 0;

while (true)
{
    string pipeName = "Satori";
    await Console.Error.WriteLineAsync($"\nWaiting for client to make on {pipeName}...\n");

    NamedPipeServerStream stream = new(pipeName, PipeDirection.InOut,
                                       NamedPipeServerStream.MaxAllowedServerInstances,
                                       PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    await stream.WaitForConnectionAsync();
    await RunAsync(stream, ++clientRequests);
}

static async Task RunAsync(NamedPipeServerStream pipe, int requestId)
{
    HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
    JsonRpc jsonRpc = new(messageHandler);
    RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
    jsonRpc.AddLocalRpcTarget(targetMetadata, new Server(), null);
    jsonRpc.StartListening();

    await Console.Error.WriteLineAsync($"  Connection request #{requestId} received.");
    await jsonRpc.Completion;
    await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");
}
