using System.IO.Pipes;
using StreamJsonRpc;

Guid guid = Guid.NewGuid();

string pipeName = "Satori";
Console.WriteLine($"Connecting to {pipeName}...");
using NamedPipeClientStream stream = new(serverName:".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await stream.ConnectAsync();
await RunAsync(stream, guid);

Console.WriteLine("Terminating stream...");

static async Task RunAsync(NamedPipeClientStream pipe, Guid guid)
{
    HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
    JsonRpc jsonRpc = new(messageHandler);
    IServer proxy = jsonRpc.Attach<IServer>();
    jsonRpc.StartListening();

    Console.WriteLine($"  ClientId: {guid}");
    bool connected = await jsonRpc.InvokeAsync<bool>("ConnectAsync", guid);

    if (connected)
    {
        int i = Random.Shared.Next(0, 10);
        int j = Random.Shared.Next(0, 10);
        int sum = await proxy.AddAsync(i, j);
        Console.WriteLine($"  Calculating {i} + {j} = {sum}");
    }
}
