using System.IO.Pipes;
using StreamJsonRpc;

Console.WriteLine("Connecting to server...");
using NamedPipeClientStream stream = new(serverName:".",
                                         "StreamJsonRpcSamplePipe",
                                         PipeDirection.InOut,
                                         PipeOptions.Asynchronous);
await stream.ConnectAsync();
await RunAsync(stream);

static async Task RunAsync(NamedPipeClientStream pipe)
{
    // https://microsoft.github.io/vs-streamjsonrpc/docs/extensibility.html
    //JsonRpc jsonRpc = new(new LengthHeaderMessageHandler(sendingStream: pipe,receivingStream: pipe,NerdbankMessagePack.CreateFormatter()));

    JsonRpc jsonRpc = new(new HeaderDelimitedMessageHandler(duplexStream: pipe, SystemTextJson.CreateFormatter()));

    IServer proxy = jsonRpc.Attach<IServer>();
    jsonRpc.StartListening();

    int i = Random.Shared.Next(0, 10);
    int j = Random.Shared.Next(0, 10);
    int sum = await proxy.AddAsync(i, j);
    Console.WriteLine($"{i} + {j} = {sum}");
    Console.WriteLine("Terminating stream...");
}
