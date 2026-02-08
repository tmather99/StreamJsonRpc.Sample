using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

// Client helper functions
internal partial class Client
{
    // Register client callbacks using IStreamListener<T> marshaling
    private static async Task SubscribeToNumberDataStream(JsonRpc jsonRpc, IServer server)
    {
        jsonRpc.AllowModificationWhileListening = true;
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<INumberStreamListener>();
        NumberStreamListener numberStreamListener = new(server);
        jsonRpc.AddLocalRpcTarget(targetMetadata, numberStreamListener, null);
        jsonRpc.AllowModificationWhileListening = false;

        await numberStreamListener.Subscribe();
    }

    // Register mouse data stream listener and subscribe to mouse events
    private static async Task<MouseStreamListener> SubcribeToMouseDataStream(JsonRpc jsonRpc)
    {
        jsonRpc.AllowModificationWhileListening = true;
        IMouseDataStream mouseDataStream = jsonRpc.Attach<IMouseDataStream>();
        RpcTargetMetadata mouseTargetMetadata = RpcTargetMetadata.FromShape<IMouseStreamListener>();
        MouseStreamListener mouseStreamListener = new(mouseDataStream);
        jsonRpc.AddLocalRpcTarget(mouseTargetMetadata, mouseStreamListener, null);
        jsonRpc.AllowModificationWhileListening = false;

        await mouseStreamListener.Subscribe();
        Console.WriteLine($"  MouseStreamListener.Subscribe({mouseStreamListener.Id})");

        return mouseStreamListener;
    }

    // Register client callbacks for counter observer stream using IObservable<T> marshaling
    private static async Task SubscribeToCounterObserver(JsonRpc jsonRpc)
    {
        jsonRpc.AllowModificationWhileListening = true;
        RpcTargetMetadata counterObserverTargetMetadata = RpcTargetMetadata.FromShape<ICounterObserver>();
        ICounterObserver counterObserver = new CounterObserver();
        jsonRpc.AddLocalRpcTarget(counterObserverTargetMetadata, counterObserver, null);
        jsonRpc.AllowModificationWhileListening = false;
    }

    // Handler for server push notifications
    private static void RegisterTickHandler(JsonRpc jsonRpc, Guid guid)
    {
        // Handle push events from server.
        jsonRpc.AddLocalRpcMethod("Tick", TickHandler(guid));
    }

    // Handler for server push notifications
    private static Func<int, Task> TickHandler(Guid guid)
    {
        return async tickNumber =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Tick {guid} - #{tickNumber}");
            Console.ResetColor();
        };
    }
}