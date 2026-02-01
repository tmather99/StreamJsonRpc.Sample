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

static async Task RunAsync(NamedPipeServerStream pipe, int clientId)
{

    // https://microsoft.github.io/vs-streamjsonrpc/docs/extensibility.html
    //JsonRpc jsonRpc = new(new LengthHeaderMessageHandler(sendingStream: pipe, receivingStream: pipe, NerdbankMessagePack.CreateFormatter()));

    JsonRpc jsonRpc = new(new HeaderDelimitedMessageHandler(duplexStream:pipe, SystemTextJson.CreateFormatter()));

    RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
    jsonRpc.AddLocalRpcTarget(targetMetadata, new Server(), null);
    jsonRpc.StartListening();

    await Console.Error.WriteLineAsync($"Connection request #{clientId} received. Spinning off an async Task to cater to requests.");
    await Console.Error.WriteLineAsync($"JSON-RPC listener attached to #{clientId}. Waiting for requests...");
    await jsonRpc.Completion;
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
