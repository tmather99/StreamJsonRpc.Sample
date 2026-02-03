using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace StreamJsonRpc.Jit.Client
{
    internal class Client
    {
        public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
        {
            JsonRpc jsonRpc = new JsonRpc(pipe);

            // Subscription to filtered observable
            IDisposable filteredSubscription = null;

            try
            {
                // Handle push events from server.
                jsonRpc.AddLocalRpcMethod("Tick", TickHandler());

                // Register server RPC methods
                IServer server = jsonRpc.Attach<IServer>();

                // Register client callbacks so server can call back to us
                var numberStreamStreamListener = new NumberStreamStreamListener();
                jsonRpc.AddLocalRpcTarget(numberStreamStreamListener);

                // Handler for server push notifications.
                Func<int, Task> TickHandler()
                {
                    return async tickNumber =>
                    {
                        Console.WriteLine($"    Tick {guid} - #{tickNumber}");
                    };
                }

                jsonRpc.StartListening();

                Console.WriteLine($"  ClientId: {guid}");
                Program.isConnected = await jsonRpc.InvokeAsync<bool>("ConnectAsync", guid);

                if (Program.isConnected)
                {
                    int a = Program.rand.Next(1, 10);
                    int b = Program.rand.Next(1, 10);
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

                    filteredSubscription = await FilteredSubscriptionAsync();

                    // blocks until canceled via Ctrl+C.
                    await jsonRpc.Completion.WithCancellation(cts.Token);
                }

                async Task<IDisposable> FilteredSubscriptionAsync()
                {
                    // Apply Rx operators to the observable
                    filteredSubscription =
                        numberStreamStreamListener.Values.Where(x => x % 2 == 0)
                            .Subscribe(x =>
                            {
                                Console.WriteLine($"           -> Even number filter: {x}");
                            });

                    // Start subscription to server stream
                    await server.SubscribeToNumberStream();

                    return filteredSubscription;
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