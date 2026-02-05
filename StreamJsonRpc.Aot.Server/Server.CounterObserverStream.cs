using System.Reactive.Linq;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Number Stream functionality
public partial class Server
{
    private ICounterObserver _counterObserverStreamListener = null!;

    // random number stream generator
    private readonly Subject<int> _counterObserverStreamSubject = new();

    // for cleanup when RPC request is canceled
    private IDisposable _counterObserverSubscription = null!;

    // Server streams data to client using notifications
    public async Task SubscribeToCounterObserverStream(IObserver<int> observer, Guid oid)
    {
        Console.WriteLine($"  Client subscribed to counter observer stream oid: {oid}");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        // register the stream listener callback interface
        _jsonRpc.AllowModificationWhileListening = true;
        _counterObserverStreamListener = _jsonRpc.Attach<ICounterObserver>();
        _jsonRpc.AllowModificationWhileListening = false;

        // Simulate publishing data periodically
        Observable.Interval(TimeSpan.FromMilliseconds(300))
            .Subscribe(i =>
            {
                if (isCancel) return;
                int value = (int)i;   // sequential: 0,1,2,3,...
                _counterObserverStreamSubject.OnNext(value);
            });

        _counterObserverSubscription = _counterObserverStreamSubject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(int value)
        {
            try
            {
                if (isCancel) return;

                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"      {value, 3} -> {clientGuid}");
                Console.ResetColor();

                // Call back to client using notification
                _counterObserverStreamListener.OnNext(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to client: {ex.Message}");
            }
        }

        async void OnError(Exception error)
        {
            if (isCancel) return;

            Console.WriteLine($"Stream error: {error.Message}");
            _counterObserverStreamListener.OnError(error);
        }

        async void OnCompleted()
        {
            if (isCancel) return;

            Console.WriteLine("Stream completed");
            _counterObserverStreamListener.OnCompleted();
        }
    }
}