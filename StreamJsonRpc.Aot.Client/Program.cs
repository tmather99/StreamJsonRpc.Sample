using System.Collections.Generic;
using System.IO.Pipes;
using StreamJsonRpc;

Guid guid = Guid.NewGuid();

string pipeName = "Satori";
Console.WriteLine($"Connecting to {pipeName}...");
using NamedPipeClientStream stream = new(serverName:".",
                                         pipeName,
                                         PipeDirection.InOut,
                                         PipeOptions.Asynchronous);
await stream.ConnectAsync();
await RunAsync(stream, guid);

Console.WriteLine("Terminating stream...");

static async Task RunAsync(NamedPipeClientStream pipe, Guid guid)
{
    HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
    JsonRpc jsonRpc = new(messageHandler);
    IServer server = jsonRpc.Attach<IServer>();
    jsonRpc.StartListening();

    Console.WriteLine($"  ClientId: {guid}");
    bool connected = await server.ConnectAsync(guid);

    if (connected)
    {
        int a = Random.Shared.Next(0, 10);
        int b = Random.Shared.Next(0, 10);
        int sum = await server.AddAsync(a, b);
        Console.WriteLine($"  Calculating {a} + {b} = {sum}");

        List<string> list = await server.GetListAsync();
        Console.WriteLine($"  GetList:");
        Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

        Dictionary<Guid, DateTime> dict = await server.GetDictionaryAsync();
        Console.WriteLine($"  GetDictionary:");
        Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

        Dictionary<string, string> table = await server.GetTableAsync();
        Console.WriteLine($"  GetTable:");
        Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));
    }
}
