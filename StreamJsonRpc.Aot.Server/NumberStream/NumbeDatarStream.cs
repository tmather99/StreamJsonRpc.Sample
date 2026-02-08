using System.Reactive.Linq;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Number Stream functionality
public partial class NumberDataStream : INumberDataStream, IDisposable
{
    private INumberStreamListener _numberStreamListener = null!;

    // random number stream generator
    private readonly Subject<int> _numberStreamSubject = new();

    // for cleanup when RPC request is canceled
    private IDisposable _numberSubscription = null!;

    private JsonRpc _jsonRpc;

    public NumberDataStream(Server server)
    {
        _jsonRpc = server.jsonRpc;
    }

    public Task Subscribe(Guid clientGuid)
    {
        return SubscribeToNumberStream(clientGuid);
    }

    public Task Unsubscribe(Guid clientGuid)
    {
        return UnsubscribeFromNumberStream(clientGuid);
    }

    // Server streams data to client using notifications
    public Task SubscribeToNumberStream(Guid clientGuid)
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
                int r = Random.Shared.Next(1, 100);
                _numberStreamSubject.OnNext(r);
            });


        _numberSubscription = _numberStreamSubject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(int value)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      {value,3} -> {clientGuid}");
                Console.ResetColor();

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
            Console.WriteLine($"Stream error: {error.Message}");
            await _numberStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            Console.WriteLine("Stream completed");
            await _numberStreamListener.OnCompleted();
        }

        return Task.CompletedTask;
    }

    // Unsubscribe from mouse stream
    public Task UnsubscribeFromNumberStream(Guid clientGuid)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Unsubscribe client {clientGuid} from mouse stream.");
        Console.ResetColor();

        _numberSubscription?.Dispose();
        _numberSubscription = null!;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _numberSubscription?.Dispose();
        _numberSubscription = null!;
    }
}