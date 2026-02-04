namespace StreamJsonRpc.Reactive.Client;

using System;
using System.IO.Pipes;
using System.Reactive.Linq;
using StreamJsonRpc;

public class CounterObserver : IObserver<int>
{
    public void OnNext(int value)
    {
        if (value != -1)
        {
            Console.WriteLine($"OnNext Couter = {value}");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Value inserted from client");
        Console.ResetColor();
    }

    public void OnCompleted()
    {
        Console.WriteLine("Counter completed.");
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"Counter error: {error.Message}");
    }
}

class Program
{
    static async Task Main()
    {
        string pipeName = "rpcpipe";
        Console.WriteLine($"Connecting to {pipeName}...");
        using (var stream = new NamedPipeClientStream(serverName: ".",
                   pipeName,
                   PipeDirection.InOut,
                   PipeOptions.Asynchronous))
        {
            await stream.ConnectAsync();
            await RunAsync(stream);
        }

        Console.ReadLine();
        Console.WriteLine("Terminating stream...");
    }

    static async Task RunAsync(NamedPipeClientStream pipe)
    {
        JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe));

        IRpcService rpcService = jsonRpc.Attach<IRpcService>();
        Console.WriteLine("Connected. Sending request...");

        jsonRpc.StartListening();

        bool boolValue = await rpcService.GetBool();
        Console.WriteLine($"GetBool {boolValue}");

        IAsyncEnumerable<int> asyncEnumerable = await rpcService.GetAsyncEnumerable();
        Console.WriteLine($"GetAsyncEnumerable:");

        await foreach (int value in asyncEnumerable)
        {
            Console.WriteLine($"  {value}");
        }

        await rpcService.Subcribe(new CounterObserver());
        Console.WriteLine($"Subcribe.");

        IObserver<int> observer = await rpcService.GetObserver();
        Console.WriteLine($"GetObserver.");

        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Subscribe(i =>
            {
                observer.OnNext(-1);
            });

        await jsonRpc.Completion;
    }
}