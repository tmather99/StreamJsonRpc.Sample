using System.IO.Pipes;
using System.Reactive.Linq;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Common;
using StreamJsonRpc.Aot.Common.UserInfoStream;

namespace StreamJsonRpc.Aot.Client;

// StreamJsonRpc client-side implementation
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

            RegisterTickHandler();

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            IUserService userService = jsonRpc.Attach<IUserService>();

            // AOT Register client callbacks so server can call back to us
            RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<INumberStreamListener>();
            NumberStreamListener numberStreamListener = new();
            jsonRpc.AddLocalRpcTarget(targetMetadata, numberStreamListener, null);

            // AOT Register client callbacks for mouse stream
            RpcTargetMetadata mouseTargetMetadata = RpcTargetMetadata.FromShape<IMouseStreamListener>();
            MouseStreamListener mouseStreamListener = new();
            jsonRpc.AddLocalRpcTarget(mouseTargetMetadata, mouseStreamListener, null);

            // AOT Register client callbacks for counter observer stream
            RpcTargetMetadata counterObserverTargetMetadata = RpcTargetMetadata.FromShape<ICounterObserver>();
            ICounterObserver counterObserverStreamListener = new CounterObserver();
            jsonRpc.AddLocalRpcTarget(counterObserverTargetMetadata, counterObserverStreamListener, null);

            // Start listening for messages
            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            Program.isConnected = await server.ConnectAsync(guid);

            if (Program.isConnected)
            {
                // Test various data type marshaling
                await Check_DataType_Marshaling(server);

                // Test custom data type marshaling
                await Check_Custom_DataType_Marshaling(userService);

                // Test various data type marshaling
                await Check_Custom_DataType_Marshaling(userService);

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

            void RegisterTickHandler()
            {
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
            }
        }
        catch (OperationCanceledException)
        {
            // stop heartbeat ticks
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

        // Custom data type marshaling
        async Task Check_Custom_DataType_Marshaling(IUserService userService)
        {
            UserInfo userInfo = new() {
                Name = "Alice",
                Age = 30
            };

            userInfo = await userService.ProcessUser(userInfo, cts.Token);
            Console.WriteLine("  ProcessUser");
            Console.WriteLine($"    -> Clienbt received: {userInfo.Name}, {userInfo.Age}");

            //IAsyncEnumerable<UserInfo> list = await userService.GetList(cts.Token);
            //Console.WriteLine($"  GetList<UserInfo>:");
            //Console.WriteLine(string.Join(Environment.NewLine, list.Select((u) => $"    [{u.Name}]: {u.Age}")));
        }

        // IObserver<T> marshaling
        async Task Check_IObserver_Marshaling(IServer server)
        {
            await server.SetObserver(new CounterObserver(), cts.Token);

            //
            // Failed to deserializing JSON-RPC argument!!!!!
            //
            //    await server.SetCounterObserver(new CounterObserver(), cts.Token);
            //    await server.SetNumberStreamListener(new NumberStreamListener(), cts.Token);
            //

            Console.WriteLine($"  SetCounterObserver.");


            // Get observer from server and push values to it
            IObserver<int> observer = await server.GetObserver(cts.Token);
            Console.WriteLine($"  GetObserver.");

            Observable.Interval(TimeSpan.FromMilliseconds(500))
                .Subscribe(i =>
                {
                    observer.OnNext(-1);
                });
        }

        // IAsyncEnumerable<T> marshaling
        async Task Check_IAsyncEnumerable_Marshaling(IServer server)
        {
            // Get async stream from server and consume it
            IAsyncEnumerable<int> stream = await server.GetAsyncEnumerable(cts.Token);
            Console.WriteLine($"  GetAsyncEnumerable:");
            Console.WriteLine("    [" + string.Join(", ", await stream.ToListAsync(cts.Token)) + "]");

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

    // Implementation of IObserver<int> to receive progress updates from server
    class ProgressObserver : IObserver<int>
    {
        public void OnNext(int value) => Console.WriteLine($"      Progress: {value}%");
        public void OnCompleted() => Console.WriteLine("\n      Progress complete.");
        public void OnError(Exception error) => Console.WriteLine(error);
    }
}