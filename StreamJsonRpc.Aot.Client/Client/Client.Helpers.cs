using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

// Client helper functions
internal partial class Client
{
    // Register client callbacks using IStreamListener<T> marshaling
    private static async Task<NumberStreamListener> SubscribeToNumberDataStream(JsonRpc jsonRpc)
    {
        jsonRpc.AllowModificationWhileListening = true;
        INumberDataStream numberDataStream = jsonRpc.Attach<INumberDataStream>();
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<INumberStreamListener>();
        NumberStreamListener numberStreamListener = new(numberDataStream);
        jsonRpc.AddLocalRpcTarget(targetMetadata, numberStreamListener, null);
        jsonRpc.AllowModificationWhileListening = false;

        await numberStreamListener.Subscribe();
        Console.WriteLine($"  NumberStreamListener.Subscribe({numberStreamListener.Id})");

        return numberStreamListener;
    }

    // Register mouse data stream listener and subscribe to mouse events
    private static async Task<MouseStreamListener> SubcribeToMouseDataStream(JsonRpc jsonRpc)
    {
        jsonRpc.AllowModificationWhileListening = true;
        IMouseDataStream mouseDataStream = jsonRpc.Attach<IMouseDataStream>();
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IMouseStreamListener>();
        MouseStreamListener mouseStreamListener = new(mouseDataStream);
        jsonRpc.AddLocalRpcTarget(targetMetadata, mouseStreamListener, null);
        jsonRpc.AllowModificationWhileListening = false;

        await mouseStreamListener.Subscribe();
        Console.WriteLine($"  MouseStreamListener.Subscribe({mouseStreamListener.Id})");

        return mouseStreamListener;
    }

    // Register client callbacks for counter observer stream using IObservable<T> marshaling
    private static async Task SubscribeToCounterObserver(JsonRpc jsonRpc)
    {
        jsonRpc.AllowModificationWhileListening = true;
        RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<ICounterObserver>();
        ICounterObserver counterObserver = new CounterObserver();
        jsonRpc.AddLocalRpcTarget(targetMetadata, counterObserver, null);
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