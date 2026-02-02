using System;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace StreamJsonRpc.Reactive.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Client starting...");
            Console.WriteLine($"Runtime: {Environment.Version}");
            Console.WriteLine($"StreamJsonRpc Version: 2.24.84");
            Console.WriteLine($"System.Reactive Version: 7.0.0-preview.1");
            Console.WriteLine("Using notification-based streaming");

            using var pipe = new NamedPipeClientStream(
                ".",
                "StreamJsonRpcSample",
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            Console.WriteLine("\nConnecting to server...");
            await pipe.ConnectAsync();
            Console.WriteLine("Connected!");

            var jsonRpc = new JsonRpc(pipe, pipe);

            // Register client callbacks so server can call back to us
            var callbacks = new ClientCallbacks();
            jsonRpc.AddLocalRpcTarget(callbacks);

            jsonRpc.StartListening();

            try
            {
                // Example 1: Subscribe to server stream
                Console.WriteLine("\n=== Example 1: Subscribe to server stream ===");

                var cts1 = new CancellationTokenSource();

                // Apply Rx operators to the observable
                var filteredSubscription = callbacks.Values
                    .Where(x => x % 2 == 0)
                    .Subscribe(x => Console.WriteLine($"  -> Even number: {x}"));

                // Start subscription (server will call back to OnNextValue)
                var subscriptionTask = jsonRpc.InvokeWithCancellationAsync(
                    "SubscribeToNumberStream",
                    cancellationToken: cts1.Token);

                await Task.Delay(5000);

                // Unsubscribe
                Console.WriteLine("Unsubscribing from stream...");
                cts1.Cancel();
                filteredSubscription.Dispose();

                try
                {
                    await subscriptionTask;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Subscription cancelled successfully");
                }

                await Task.Delay(1000);

                // Example 2: Send individual values to server
                Console.WriteLine("\n=== Example 2: Sending individual values ===");
                for (int i = 100; i < 105; i++)
                {
                    Console.WriteLine($"Sending: {i}");
                    await jsonRpc.InvokeAsync("SendValueToServer", i);
                    await Task.Delay(500);
                }

                await Task.Delay(1000);

                // Example 3: Multiple subscribers to same stream
                Console.WriteLine("\n=== Example 3: Multiple subscribers ===");

                var cts3 = new CancellationTokenSource();

                // Use the same callbacks object - create multiple subscriptions to it
                var sub3a = callbacks.Values.Subscribe(x => Console.WriteLine($"Subscriber A: {x}"));
                var sub3b = callbacks.Values
                    .Where(x => x > 20)
                    .Subscribe(x => Console.WriteLine($"Subscriber B (>20): {x}"));

                var subscriptionTask3 = jsonRpc.InvokeWithCancellationAsync(
                    "SubscribeToNumberStream",
                    cancellationToken: cts3.Token);

                await Task.Delay(5000);

                // Cleanup
                Console.WriteLine("\nCleaning up...");
                cts3.Cancel();
                sub3a.Dispose();
                sub3b.Dispose();

                try
                {
                    await subscriptionTask3;
                }
                catch (OperationCanceledException) { }

                await Task.Delay(1000);

                // Example 4: Broadcast value that all subscribers receive
                Console.WriteLine("\n=== Example 4: Broadcasting values ===");

                var cts4 = new CancellationTokenSource();

                var sub4 = callbacks.Values.Subscribe(x => Console.WriteLine($"Received broadcast: {x}"));

                var subscriptionTask4 = jsonRpc.InvokeWithCancellationAsync(
                    "SubscribeToNumberStream",
                    cancellationToken: cts4.Token);

                await Task.Delay(2000);

                // Send some values that will be broadcast to all subscribers
                for (int i = 200; i < 203; i++)
                {
                    Console.WriteLine($"Broadcasting: {i}");
                    await jsonRpc.InvokeAsync("BroadcastValue", i);
                    await Task.Delay(300);
                }

                await Task.Delay(2000);

                cts4.Cancel();
                sub4.Dispose();

                try
                {
                    await subscriptionTask4;
                }
                catch (OperationCanceledException) { }

                // Example 5: Using Rx operators
                Console.WriteLine("\n=== Example 5: Rx operators (buffer) ===");

                var cts5 = new CancellationTokenSource();

                // Buffer values and process in batches
                var sub5 = callbacks.Values
                    .Buffer(TimeSpan.FromSeconds(3))
                    .Where(buffer => buffer.Count > 0)
                    .Subscribe(buffer => Console.WriteLine($"Buffered {buffer.Count} values: {string.Join(", ", buffer)}"));

                var subscriptionTask5 = jsonRpc.InvokeWithCancellationAsync(
                    "SubscribeToNumberStream",
                    cancellationToken: cts5.Token);

                await Task.Delay(7000);

                cts5.Cancel();
                sub5.Dispose();

                try
                {
                    await subscriptionTask5;
                }
                catch (OperationCanceledException) { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n!!! ERROR !!!");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\nInner exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\n\nPress any key to exit...");
            Console.ReadKey();

            jsonRpc.Dispose();
        }
    }
}