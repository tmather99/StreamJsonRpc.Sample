using System.IO.Pipes;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

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
            RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<INumberStreamListener>();
            NumberStreamListener numberStreamListener = new();
            jsonRpc.AddLocalRpcTarget(targetMetadata, numberStreamListener, null);

            // Register client callbacks for mouse stream
            RpcTargetMetadata mouseTargetMetadata = RpcTargetMetadata.FromShape<IMouseStreamListener>();
            MouseStreamListener mouseStreamListener = new();
            jsonRpc.AddLocalRpcTarget(mouseTargetMetadata, mouseStreamListener, null);

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
            await jsonRpc.InvokeAsync("CancelTickOperation", guid);
            numberSubscription?.Dispose();
            mouseClickSubscription?.Dispose();
            mouseMoveSubscription?.Dispose();
            throw;  // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        // StreamJsonRpc object marshaling
        async Task Check_DataType_Marshaling(IServer server)
        {
            // int marshaling
            int a = Random.Shared.Next(0, 10);
            int b = Random.Shared.Next(0, 10);
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
            await server.SetObserver(new CounterObserver(), cts.Token);
            Console.WriteLine($"SetObserver.");

            IObserver<int> observer = await server.GetObserver(cts.Token);
            Console.WriteLine($"GetObserver.");

            Observable.Interval(TimeSpan.FromMilliseconds(500))
                .Subscribe(i =>
                {
                    observer.OnNext(-1);
                });
        }

        // IAsyncEnumerable<T> marshaling
        async Task Check_IAsyncEnumerable_Marshaling(IServer server)
        {
            IAsyncEnumerable<int> stream = await server.GetAsyncEnumerable(cts.Token);
            Console.WriteLine($"  GetAsyncEnumerable:");
            Console.WriteLine("    [" + string.Join(", ", await stream.ToListAsync(cts.Token)) + "]");

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
        }
    }
}   