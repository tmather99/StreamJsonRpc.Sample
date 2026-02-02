using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using StreamJsonRpc;

class Program
{
    static Random rand = new Random();
    static Guid guid = Guid.NewGuid();

    static async Task Main(string[] args)
    {
        string pipeName = "Satori";
        Console.WriteLine($"Connecting to {pipeName}...");
        using (var stream = new NamedPipeClientStream(serverName: ".",
                                                      pipeName, 
                                                      PipeDirection.InOut,
                                                      PipeOptions.Asynchronous))
        {
            await stream.ConnectAsync();
            await RunAsync(stream);
        }

        Console.WriteLine("Terminating stream...");
    }

    static async Task RunAsync(NamedPipeClientStream stream)
    {
        var jsonRpc = JsonRpc.Attach(stream);

        Console.WriteLine($"  ClientId: {guid}");
        bool connected = await jsonRpc.InvokeAsync<bool>("ConnectAsync", guid);

        if (connected)
        {
            int a = rand.Next(1, 10);
            int b = rand.Next(1, 10);
            int sum = await jsonRpc.InvokeAsync<int>("AddAsync", a, b);
            Console.WriteLine($"  Calculating {a} + {b} = {sum}");

            List<string> list = await jsonRpc.InvokeAsync<List<string>>("GetListAsync");
            Console.WriteLine($"  GetList:");
            Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

            Dictionary<Guid, DateTime> dict = await jsonRpc.InvokeAsync<Dictionary<Guid, DateTime>>("GetDictionaryAsync");
            Console.WriteLine($"  GetDictionary:");
            Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

            Dictionary<string, string> table = await jsonRpc.InvokeAsync<Dictionary<string, string>>("GetTableAsync");
            Console.WriteLine($"  GetTable:");
            Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));
        }
    }
}
