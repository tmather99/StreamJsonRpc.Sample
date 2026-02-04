using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace StreamJsonRpc.Jit.Client;

internal class Client
{
    public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
    {
        // Create the JSON-RPC client over the pipe with MessagePack serialization
        JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe));

        // Subscription to filtered observable
        IDisposable numberSubscription = null;
        IDisposable mouseClickSubscription = null;
        IDisposable mouseMoveSubscription = null;

        try
        {
            // Handle push events from server.
            jsonRpc.AddLocalRpcMethod("Tick", TickHandler());

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register client callbacks so server can call back to us
            var numberStreamStreamListener = new NumberStreamListener();
            jsonRpc.AddLocalRpcTarget(numberStreamStreamListener);

            // Handler for server push notifications.
            Func<int, Task> TickHandler()
            {
                return async tickNumber =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    Tick {guid} - #{tickNumber}");
                    Console.ResetColor();
                };
            }

            // Register client callbacks for mouse stream
            var mouseStreamListener = new MouseStreamListener();
            jsonRpc.AddLocalRpcTarget(mouseStreamListener);

            // Start listening for messages
            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            Program.isConnected = await server.ConnectAsync(guid);

            if (Program.isConnected)
            {
                // Test various data type marshaling
                await CheckDataTypeMarshaling(server);

                // Start server hearbeat ticks
                await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                Console.WriteLine($"  SendTicksAsync {guid}");

                // Subscribe to filtered number stream
                numberSubscription = numberStreamStreamListener.CreateFilteredSubscription();

                // Start subscription to server stream
                await server.SubscribeToNumberStream();

                // Subscribe to mouse events
                mouseClickSubscription = mouseStreamListener.CreateClickSubscription();
                mouseMoveSubscription = mouseStreamListener.CreateMovementSubscription();

                // Register to mouse events
                await server.SubscribeToMouseStream();

                // blocks until canceled via Ctrl+C.
                await jsonRpc.Completion.WithCancellation(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            await jsonRpc.InvokeAsync("CancelTickOperation", guid);
            mouseClickSubscription?.Dispose();
            mouseMoveSubscription?.Dispose();
            throw;  // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }

        // StreamJsonRpc object marshaling
        async Task CheckDataTypeMarshaling(IServer server)
        {
            int a = Program.rand.Next(1, 10);
            int b = Program.rand.Next(1, 10);
            int sum = await server.AddAsync(a, b);
            Console.WriteLine($"  Calculating {a} + {b} = {sum}");

            List<string> list = await server.GetListAsync();
            Console.WriteLine($"  GetList:");
            Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

            Dictionary<Guid, DateTime> dict = await server.GetDictionaryAsync();
            Console.WriteLine($"  GetDictionary:");
            Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

            Dictionary<string, string> table = await server.GetTableAsync();
            Console.WriteLine($"  GetTable:");
            Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));

            IAsyncEnumerable<int> stream = await server.GetAsyncEnumerable(cts.Token);
            Console.WriteLine($"  GetAsyncEnumerable:");
            Console.WriteLine("    [" + string.Join(", ", await ToListAsync(stream, cts.Token)) + "]");

            await server.SetAsyncEnumerable(ProduceNumbers(cts.Token), cts.Token);
            Console.WriteLine($"  SetAsyncEnumerable.");

            async IAsyncEnumerable<int> ProduceNumbers([EnumeratorCancellation] CancellationToken ct = default)
            {
                for (int i = 1; i <= 10; i++)
                {
                    await Task.Delay(100, ct);   // simulate async work
                    yield return i;
                }
            }

            await server.SetObserver(new CounterObserver());
            Console.WriteLine($"SetObserver.");

            IObserver<int> observer = await server.GetObserver();
            Console.WriteLine($"GetObserver.");

            Observable.Interval(TimeSpan.FromMilliseconds(500))
                .Subscribe(i =>
                {
                    observer.OnNext(-1);
                });
        }
    }

    // Add this helper method to the Client class (or as a static utility method)
    private static async Task<List<int>> ToListAsync(IAsyncEnumerable<int> source, CancellationToken cancellationToken = default)
    {
        var list = new List<int>();
        await foreach (int item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }
        return list;
    }
}