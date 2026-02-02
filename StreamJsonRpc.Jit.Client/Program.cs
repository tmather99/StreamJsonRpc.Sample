using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

class Program
{
    static Random rand = new Random();
    static Guid guid = Guid.NewGuid();

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

            jsonRpc.StartListening();

            Console.WriteLine($"  ClientId: {guid}");
            bool connected = await jsonRpc.InvokeAsync<bool>("ConnectAsync", guid);

            if (connected)
            {
                int a = rand.Next(1, 10);
                int b = rand.Next(1, 10);
                int sum = await jsonRpc.InvokeAsync<int>("AddAsync", a, b);
                Console.WriteLine($"  Calculating {a} + {b} = {sum}");

                List<string> list = await jsonRpc.InvokeAsync<List<string>>("GetListAsync");
                Console.WriteLine($"  GetList:");
                Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

                Dictionary<Guid, DateTime> dict = await jsonRpc.InvokeAsync<Dictionary<Guid, DateTime>>("GetDictionaryAsync");
                Console.WriteLine($"  GetDictionary:");
                Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

                Dictionary<string, string> table = await jsonRpc.InvokeAsync<Dictionary<string, string>>("GetTableAsync");
                Console.WriteLine($"  GetTable:");
                Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));

                await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                Console.WriteLine($"  SendTicksAsync {guid}");

                // blocks until canceled via Ctrl+C.
                await jsonRpc.Completion.WithCancellation(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            await jsonRpc.InvokeAsync("CancelTickOperation", guid);
            throw;  // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
