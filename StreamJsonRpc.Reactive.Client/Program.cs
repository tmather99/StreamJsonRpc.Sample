namespace StreamJsonRpc.Reactive.Client;

using System;
using System.IO.Pipes;
using System.Reactive.Linq;
using StreamJsonRpc;

public class CounterObserver : IObserver<int>
{
    public void OnNext(int value)
    {
        Console.WriteLine($"OnNext Couter = {value}");
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

        Console.WriteLine("Terminating stream...");
    }

    static async Task RunAsync(NamedPipeClientStream pipe)
    {
        HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
        JsonRpc jsonRpc = new(messageHandler);

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

        //
        // IObserver and IObserable are not marshalable by StreamJsonRpc.Reactive
        //
        await rpcService.Subcribe(new CounterObserver());
        Console.WriteLine($"Subcribe.");

        IObservable<int> observer = await rpcService.GetObservable();
        IDisposable subscription = observer
            .Subscribe(
                onNext: value => Console.WriteLine($"OnNext Observable = {value}"),
                onError: error => Console.WriteLine($"Observable error: {error.Message}"),
                onCompleted: () => Console.WriteLine("Observable completed."));
        Console.WriteLine($"GetObservable.");

    }
}