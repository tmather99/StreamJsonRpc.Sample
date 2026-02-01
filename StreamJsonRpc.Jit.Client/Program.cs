using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Microsoft;
using StreamJsonRpc;

class Program
{
    static Random rand = new Random();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Connecting to server...");
        using (NamedPipeClientStream stream = 
               new NamedPipeClientStream(serverName: ".",
                                         "StreamJsonRpcSamplePipe",
                                         PipeDirection.InOut,
                                         PipeOptions.Asynchronous))
        {
            await stream.ConnectAsync();
            await RunAsync(stream);
        }
    }

    static async Task RunAsync(NamedPipeClientStream stream)
    {
        using (var jsonRpc = JsonRpc.Attach(stream))
        {
            int i = rand.Next(1, 10);
            int j = rand.Next(1, 10);
            int sum = await jsonRpc.InvokeAsync<int>("AddAsync", i, j);
            Console.WriteLine($"{i} + {j} = {sum}");
            Console.WriteLine("Terminating stream...");
        }
    }
}
