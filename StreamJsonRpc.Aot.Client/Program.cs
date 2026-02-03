using System.IO.Pipes;
using System.Reactive.Linq;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Client;

internal class Program
{
    static bool isConnected = false;
    static Guid guid = Guid.NewGuid();

    public static async Task Main(string[] args)
    {
        string pipeName = "Satori";
        Console.WriteLine($"Connecting to {pipeName}...");
        using NamedPipeClientStream stream = new(serverName:".",
                                                 pipeName,
                                                 PipeDirection.InOut,
                                                 PipeOptions.Asynchronous);
        // Setup Ctrl+C cancellation.
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            if (!isConnected)
            {
                Console.WriteLine("Not connected yet, exiting immediately.");
                return;
            }

            cts.Cancel();
            e.Cancel = true;
            Console.WriteLine("Canceling...");
        };

        try
        {
            // Connect to server.
            await stream.ConnectAsync();
            await RunAsync(stream, guid, cts);
            Console.WriteLine("\nPress Ctrl+C to end.\n");
        }
        catch (OperationCanceledException)
        {
            // This is the normal way we close.
            Console.WriteLine("Operation canceled.");
        }
        finally
        {
            Console.WriteLine("Terminating stream...");
        }

        // Client RPC operations and server notification handler.
        static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
        {
            HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
            JsonRpc jsonRpc = new(messageHandler);

            IDisposable? filteredSubscription = null;

            try
            {
                // Handle push events from server.
                jsonRpc.AddLocalRpcMethod("Tick", TickHandler(guid));

                // Handler for server push notifications.
                Func<int, Task> TickHandler(Guid guid)
                {
                    return async tickNumber =>
                    {
                        Console.WriteLine($"    Tick {guid} - #{tickNumber}");
                    };
                }

                // Register server RPC methods
                IServer server = jsonRpc.Attach<IServer>();

                // Register client callbacks so server can call back to us
                RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IListener>();
                var listener = new Listener();
                jsonRpc.AddLocalRpcTarget(targetMetadata, listener, null);

                // Start listening for messages
                jsonRpc.StartListening();

                Console.WriteLine($"  ClientId: {guid}");
                isConnected = await server.ConnectAsync(guid);

                if (isConnected)
                {
                    int a = Random.Shared.Next(0, 10);
                    int b = Random.Shared.Next(0, 10);
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

                    // Start server hearbeat ticks
                    await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                    Console.WriteLine($"  SendTicksAsync {guid}");

                    // Apply Rx operators to the observable
                    filteredSubscription = 
                        listener.Values.Where(x => x % 2 == 0)
                            .Subscribe(x => Console.WriteLine($"           -> Even number filter: {x}"));

                    // Start subscription to server stream
                    await server.SubscribeToNumberStream();
            
                    // blocks until canceled via Ctrl+C.
                    await jsonRpc.Completion.WithCancellation(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                await jsonRpc.InvokeAsync("CancelTickOperation", guid);
                filteredSubscription?.Dispose();
                throw;  // rethrow to main
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
