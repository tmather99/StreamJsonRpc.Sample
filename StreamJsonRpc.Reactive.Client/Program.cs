using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Reactive.Linq;
using StreamJsonRpc;

class CounterObserver : IObserver<int>
{
    public void OnNext(int value) => Console.WriteLine($"[Counter] {value}");
    public void OnCompleted() => Console.WriteLine("Counter completed.");
    public void OnError(Exception error) => Console.WriteLine($"Counter error: {error.Message}");
}

class Program
{
    static async Task Main()
    {
        var pipe = new NamedPipeClientStream(".", "rpcpipe",
            PipeDirection.InOut, PipeOptions.Asynchronous);

        await pipe.ConnectAsync();

        JsonRpc rpc = JsonRpc.Attach(pipe);
        IRpcService service = rpc.Attach<IRpcService>();

        // 🔹 1. Send IObserver<T> to server
        service.SubscribeCounter(new CounterObserver());

        Console.WriteLine("Press ENTER to stop...");
        Console.ReadLine();
    }
}