using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Jit.Client;

class Program
{
    static public bool isConnected = false;
    static Guid guid = Guid.NewGuid();
    static Random rand = new Random();

    static async Task Main(string[] args)
    {
        string pipeName = "Satori";

        Console.WriteLine($"Connecting to {pipeName}...");
        using (var stream = new NamedPipeClientStream(serverName: ".",
                                                      pipeName, 
                                                      PipeDirection.InOut,
                                                      PipeOptions.Asynchronous))
        {
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
                await stream.ConnectAsync();
                await RunAsync(stream, cts);
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
        }
    }

    // Client RPC operations and server notification handler.
    static async Task RunAsync(NamedPipeClientStream pipe, CancellationTokenSource cts)
    {
        JsonRpc jsonRpc = new JsonRpc(pipe);

        // Subscription to filtered observable
        IDisposable filteredSubscription = null;

        try
        {
            // Handle push events from server.
            jsonRpc.AddLocalRpcMethod("Tick", TickHandler(guid));

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register client callbacks so server can call back to us
            Listener listener = new Listener();
            jsonRpc.AddLocalRpcTarget(listener);

            // Handler for server push notifications.
            Func<int, Task> TickHandler(Guid guid)
            {
                return async tickNumber =>
                {
                    Console.WriteLine($"    Tick {guid} - #{tickNumber}");
                };
            }

            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            isConnected = await jsonRpc.InvokeAsync<bool>("ConnectAsync", guid);

            if (isConnected)
            {
                int a = rand.Next(1, 10);
                int b = rand.Next(1, 10);
                int sum = await jsonRpc.InvokeAsync<int>("AddAsync", a, b);
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

                await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                Console.WriteLine($"  SendTicksAsync {guid}");

                // Apply Rx operators to the observable
                filteredSubscription = listener.Values.Where(x => x % 2 == 0)
                    .Subscribe(x =>
                    {
                        Console.WriteLine($"           -> Even number filter: {x}");
                    });

                // Start subscription to server stream
                var subscriptionTask = server.SubscribeToNumberStream();

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
