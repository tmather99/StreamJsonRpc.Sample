using System.IO.Pipes;
using System.Reactive.Linq;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

internal class Client
{
    public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
    {
        HeaderDelimitedMessageHandler messageHandler = new(pipe, SystemTextJson.CreateFormatter());
        JsonRpc jsonRpc = new(messageHandler);

        // Subscription to filtered observable
        IDisposable? filteredSubscription = null;

        try
        {
            // Handle push events from server.
            jsonRpc.AddLocalRpcMethod("Tick", TickHandler());

            // Handler for server push notifications.
            Func<int, Task> TickHandler()
            {
                return async tickNumber =>
                {
                    Console.WriteLine($"    Tick {guid} - #{tickNumber}");
                };
            }

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register client callbacks so server can call back to us
            RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<INumberStreamStreamListener>();
            NumberStreamStreamListener numberStreamStreamListener = new();
            jsonRpc.AddLocalRpcTarget(targetMetadata, numberStreamStreamListener, null);

            // Start listening for messages
            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            Program.isConnected = await server.ConnectAsync(guid);

            if (Program.isConnected)
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
                    numberStreamStreamListener.Values.Where(x => x % 2 == 0)
                        .Subscribe(x =>
                        {
                            Console.WriteLine($"           -> Even number filter: {x}");
                        });

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