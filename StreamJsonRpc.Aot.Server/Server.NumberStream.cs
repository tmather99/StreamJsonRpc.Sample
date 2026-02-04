using System.Reactive.Linq;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Number Stream functionality
public partial class Server
{
    private INumberStreamListener _numberStreamListener = null!;

    // random number stream generator
    private readonly Subject<int> _randomNumberGenerator = new();

    // for cleanup when RPC request is canceled
    private IDisposable _numberSubscription = null!;

    // Server streams data to client using notifications
    public async Task SubscribeToNumberStream()
    {
        Console.WriteLine("  Client subscribed to number stream");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        // register the stream listener callback interface
        _jsonRpc.AllowModificationWhileListening = true;
        _numberStreamListener = _jsonRpc.Attach<INumberStreamListener>();
        _jsonRpc.AllowModificationWhileListening = false;

        // Simulate publishing data periodically
        Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Subscribe(i =>
            {
                if (isCancel) return;
                int r = Random.Shared.Next(1, 100);
                _randomNumberGenerator.OnNext(r);
            });


        _numberSubscription = _randomNumberGenerator.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(int value)
        {
            try
            {
                Console.WriteLine($"      {value, 3} -> {clientGuid}");

                if (isCancel) return;

                // Call back to client using notification
                await _numberStreamListener.OnNextValue(value);
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
            await _numberStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            if (isCancel) return;

            Console.WriteLine("Stream completed");
            await _numberStreamListener.OnCompleted();
        }
    }
}