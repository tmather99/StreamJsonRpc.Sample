using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Reactive.Linq;
using StreamJsonRpc;

class RpcService : IRpcService
{
    public void SubscribeCounter(IObserver<int> observer)
    {
        Console.WriteLine("Client subscribed to counter.");

        var sub = Observable.Interval(TimeSpan.FromSeconds(1))
            .Select(x =>
            {
                var value = (int)x + 1;
                Console.WriteLine($"Select produced: {value}");
                return value;
            })
            .Subscribe(observer);
    }
}

class Program
{
    static async Task Main()
    {
        var server = new NamedPipeServerStream("rpcpipe", PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        Console.WriteLine("Waiting for client...");
        await server.WaitForConnectionAsync();

        JsonRpc rpc = JsonRpc.Attach(server, new RpcService());

        Console.WriteLine("Server ready.");
        await rpc.Completion;
    }
}