using System.Reactive.Linq;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

// Client IObserver<T> marshaling tests
internal partial class Client
{
    // IObserver<T> marshaling
    private static async Task Check_IObserver_Marshaling(IServer server, CancellationTokenSource cts)
    {
        await server.SetObserver(new CounterObserver(), cts.Token);

        //
        // Failed to deserializing JSON-RPC argument!!!!!
        //
        //    await server.SetCounterObserver(new CounterObserver(), cts.Token);
        //    await server.SetNumberStreamListener(new NumberStreamListener(), cts.Token);
        //

        Console.WriteLine($"  SetCounterObserver.");

        // Get observer from server and push values to it
        IObserver<int> observer = await server.GetObserver(cts.Token);
        Console.WriteLine($"  GetObserver.");

        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Subscribe(i =>
            {
                observer.OnNext(-1);
            });
    }
}