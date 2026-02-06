using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace StreamJsonRpc.Jit.Client;

internal class Client
{
    public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
    {
        JsonRpc jsonRpc = null!;

        // Subscription to filtered observable
        IDisposable? numberSubscription = null;
        IDisposable? mouseClickSubscription = null;
        IDisposable? mouseMoveSubscription = null;

        try
        {
            // Create the MessagePack handler over the pipe
            jsonRpc = new(MessagePackHandler.Create(pipe));

            // Handle push events from server.
            jsonRpc.AddLocalRpcMethod("Tick", TickHandler());

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

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register client callbacks so server can call back to us
            NumberStreamListener numberStreamListener = new();
            jsonRpc.AddLocalRpcTarget(numberStreamListener);

            // Register client callbacks for mouse stream
            MouseStreamListener mouseStreamListener = new();
            jsonRpc.AddLocalRpcTarget(mouseStreamListener);

            IObserver<int> counterObserver = new CounterObserver();
            jsonRpc.AddLocalRpcTarget(counterObserver);

            // AOT Register client callbacks for mouse stream
            RpcTargetMetadata counterObserverTargetMetadata = RpcTargetMetadata.FromShape<ICounterObserver>();
            jsonRpc.AddLocalRpcTarget(counterObserverTargetMetadata, counterObserver, null);

            // Start listening for messages
            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            Program.isConnected = await server.ConnectAsync(guid);

            if (Program.isConnected)
            {
                // Test various data type marshaling
                await Check_DataType_Marshaling(server);

                // Start server hearbeat ticks
                await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                Console.WriteLine($"  SendTicksAsync {guid}");

                // Subscribe to filtered number stream
                numberSubscription = numberStreamListener.CreateFilteredSubscription();

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
            // stop hearbeat ticks
            await jsonRpc.InvokeAsync("CancelTickOperation", guid);

            numberSubscription?.Dispose();
            mouseClickSubscription?.Dispose();
            mouseMoveSubscription?.Dispose();
            throw; // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }

        // StreamJsonRpc object marshaling
        async Task Check_DataType_Marshaling(IServer server)
        {
            // int marshaling
            int a = Program.rand.Next(1, 10);
            int b = Program.rand.Next(1, 10);
            int sum = await server.AddAsync(a, b);
            Console.WriteLine($"  Calculating {a} + {b} = {sum}");

            // List<string> marshaling
            List<string> list = await server.GetListAsync();
            Console.WriteLine($"  GetList:");
            Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

            // Dictionary<Guid, DateTime> marshaling
            Dictionary<Guid, DateTime> dict = await server.GetDictionaryAsync();
            Console.WriteLine($"  GetDictionary:");
            Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

            // Dictionary<string, string> marshaling
            Dictionary<string, string> table = await server.GetTableAsync();
            Console.WriteLine($"  GetTable:");
            Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));

            // IObserver<T> marshaling
            await Check_IObserver_Marshaling(server);

            // IAsyncEnumerable<T> marshaling
            await Check_IAsyncEnumerable_Marshaling(server);
        }

        // IObserver<T> marshaling
        async Task Check_IObserver_Marshaling(IServer server)
        {
            ICounterObserver counterObserver = new CounterObserver();

            //await server.SetObserver(counterObserver, cts.Token);
            //Console.WriteLine($"SetObserver.");

            await server.SetCounterObserver(counterObserver, Guid.NewGuid(), cts.Token);
            Console.WriteLine($"  SetCounterObserver.");

            IObserver<int> observer = await server.GetObserver(cts.Token);
            Console.WriteLine($"  GetObserver.");

            Observable.Interval(TimeSpan.FromMilliseconds(500))
                .Subscribe(i => { observer.OnNext(-1); });
        }

        // IAsyncEnumerable<T> marshaling
        async Task Check_IAsyncEnumerable_Marshaling(IServer server)
        {
            // Get stream from server and consume it
            IAsyncEnumerable<int> stream = await server.GetAsyncEnumerable(cts.Token);
            Console.WriteLine($"  GetAsyncEnumerable:");
            Console.WriteLine("    [" + string.Join(", ", await ToListAsync(stream, cts.Token)) + "]");

            await server.SetAsyncEnumerable(ProduceNumbers(cts.Token), cts.Token);
            Console.WriteLine($"  SetAsyncEnumerable.");

            async IAsyncEnumerable<int> ProduceNumbers(CancellationToken ct = default)
            {
                for (int i = 1; i <= 10; i++)
                {
                    await Task.Delay(100, ct);   // simulate async work
                    yield return i;
                }
            }

            // Push async stream to server and process it with client-side progress reporting
            await ProcessAsyncEnumerable(server);
        }

        // Push async stream to server
        async Task ProcessAsyncEnumerable(IServer server)
        {
            // Process server stream with client-side progress reporting
            IAsyncEnumerable<int> valueStream = await server.ProcessAsyncEnumerable(new ProgressObserver(), cts.Token);
            Console.WriteLine($"  ProcessAsyncEnumerable.");

            await foreach (int item in valueStream.WithCancellation(cts.Token))
            {
                Console.WriteLine($"    Client received stream value: {item}");
            }

            Console.WriteLine("\n    -- Server stream completed successfully --\n");
        }
    }

    // net48 does not have ToListAsync extension method
    private static async Task<List<int>> ToListAsync(IAsyncEnumerable<int> source, CancellationToken ct = default)
    {
        var list = new List<int>();
        await foreach (int item in source.WithCancellation(ct))
        {
            list.Add(item);
        }

        return list;
    }

    // Implementation of IObserver<int> to receive progress updates from server
    class ProgressObserver : IObserver<int>
    {
        //
        // NOTE: progress is not received for net48 client.
        //
        public void OnNext(int value) => Console.WriteLine($"      Progress: {value}%");
        public void OnCompleted() => Console.WriteLine("\n      Progress complete.");
        public void OnError(Exception error) => Console.WriteLine(error);
    }
}
