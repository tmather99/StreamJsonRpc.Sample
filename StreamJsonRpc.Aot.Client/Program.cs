using System.IO.Pipes;
using StreamJsonRpc;

Console.WriteLine("Connecting to server...");
using NamedPipeClientStream stream = new(serverName:".",
                                         "StreamJsonRpcSamplePipe",
                                         PipeDirection.InOut,
                                         PipeOptions.Asynchronous);
await stream.ConnectAsync();
await RunAsync(stream);

static async Task RunAsync(NamedPipeClientStream clientPipe)
{
    //JsonRpc clientRpc = new(new HeaderDelimitedMessageHandler(clientPipe, SystemTextJson.CreateFormatter()));
    JsonRpc clientRpc = new(new LengthHeaderMessageHandler(clientPipe, clientPipe, NerdbankMessagePack.CreateFormatter()));
    IServer proxy = clientRpc.Attach<IServer>();
    clientRpc.StartListening();

    int i = Random.Shared.Next(0, 10);
    int j = Random.Shared.Next(0, 10);
    int sum = await proxy.AddAsync(i, j);
    Console.WriteLine($"{i} + {j} = {sum}");
    Console.WriteLine("Terminating stream...");
}
