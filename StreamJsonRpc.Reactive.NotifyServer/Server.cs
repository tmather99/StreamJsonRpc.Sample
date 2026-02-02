using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Reactive.Server
{
    public class Server
    {
        private readonly Subject<int> _subject = new Subject<int>();
        private int _counter = 0;
        private JsonRpc? _clientRpc;

        public void SetClientRpc(JsonRpc clientRpc)
        {
            _clientRpc = clientRpc;
        }

        public Server()
        {
            // Simulate publishing data periodically
            Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(i => _subject.OnNext(_counter++));
        }

        // Server streams data to client using notifications
        public async Task SubscribeToNumberStream(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Client subscribed to number stream");

            if (_clientRpc == null)
            {
                throw new InvalidOperationException("Client RPC not set");
            }

            var subscription = _subject.Subscribe(
                async value =>
                {
                    try
                    {
                        // Call back to client using notification
                        await _clientRpc.NotifyAsync("OnNextValue", value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending to client: {ex.Message}");
                    }
                },
                async error =>
                {
                    Console.WriteLine($"Stream error: {error.Message}");
                    await _clientRpc.NotifyAsync("OnError", error.Message);
                },
                async () =>
                {
                    Console.WriteLine("Stream completed");
                    await _clientRpc.NotifyAsync("OnCompleted");
                });

            try
            {
                // Keep subscription alive until cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Client unsubscribed from number stream");
            }
            finally
            {
                subscription.Dispose();
            }
        }

        // Client sends individual values to server
        public Task SendValueToServer(int value)
        {
            Console.WriteLine($"Server received from client: {value}");
            return Task.CompletedTask;
        }

        // Broadcast value to all subscribers
        public Task BroadcastValue(int value)
        {
            Console.WriteLine($"Server broadcasting value: {value}");
            _subject.OnNext(value);
            return Task.CompletedTask;
        }
    }
}