using System.IO.Pipes;
using StreamJsonRpc;

int clientId = 0;

while (true)
{
    await Console.Error.WriteLineAsync("Waiting for client to make a connection...");

    NamedPipeServerStream stream = new("StreamJsonRpcSamplePipe", PipeDirection.InOut,
                                        NamedPipeServerStream.MaxAllowedServerInstances,
                                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    await stream.WaitForConnectionAsync();
    await RunAsync(stream, ++clientId);
}

static async Task RunAsync(NamedPipeServerStream serverPipe, int clientId)
{
    JsonRpc serverRpc = new(new HeaderDelimitedMessageHandler(serverPipe, SystemTextJson.CreateFormatter()));
    RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
    serverRpc.AddLocalRpcTarget(targetMetadata, new Server(), null);
    serverRpc.StartListening();

    await Console.Error.WriteLineAsync($"Connection request #{clientId} received. Spinning off an async Task to cater to requests.");
    await Console.Error.WriteLineAsync($"JSON-RPC listener attached to #{clientId}. Waiting for requests...");
    await serverRpc.Completion;
    await Console.Error.WriteLineAsync($"Connection #{clientId} terminated.\n");
}

// Server implementation
internal class Server : IServer
{
    public Task<int> AddAsync(int a, int b)
    {
        int sum = a + b;
        return Task.FromResult(sum);
    }
}
