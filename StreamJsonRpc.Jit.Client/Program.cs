using System;
using System.IO.Pipes;
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
        using (var stream = new NamedPipeClientStream(serverName: ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
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
            int i = rand.Next(1, 10);
            int j = rand.Next(1, 10);
            int sum = await jsonRpc.InvokeAsync<int>("AddAsync", i, j);
            Console.WriteLine($"  Calculating {i} + {j} = {sum}");
        }
    }
}
