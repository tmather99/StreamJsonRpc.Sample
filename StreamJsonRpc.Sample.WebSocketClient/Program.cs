using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace StreamJsonRpc.Sample.WebSocketClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                cts.Cancel();
                e.Cancel = true;
                Console.WriteLine("Canceling...");
            };

            try
            {
                Console.WriteLine("Press Ctrl+C to end.");
                await MainAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // This is the normal way we close.
                Console.WriteLine("Operation canceled.");
            }
            finally
            {
                Console.WriteLine("finally!");
            }
        }

        static async Task MainAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Connecting to web socket...");
            using (var socket = new ClientWebSocket())
            {
                Guid guid = Guid.NewGuid();

                await socket.ConnectAsync(new Uri("wss://localhost:5001/socket"), cancellationToken);
                Console.WriteLine("Connected to web socket. Establishing JSON-RPC protocol...");
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket)))
                {
                    try
                    {
                        jsonRpc.AddLocalRpcMethod("Tick", TickHandler(guid, jsonRpc));
                        jsonRpc.StartListening();

                        Console.WriteLine("JSON-RPC protocol over web socket established.");
                        int result = await jsonRpc.InvokeWithCancellationAsync<int>("Add", [1, 2], cancellationToken);
                        Console.WriteLine($"JSON-RPC server says 1 + 2 = {result}");
                        
                        // Request notifications from the server.
                        await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                        await jsonRpc.Completion.WithCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        await jsonRpc.InvokeAsync("CancelTickOperation", guid);

                        // Closing is initiated by Ctrl+C on the client.
                        // Close the web socket gracefully -- before JsonRpc is disposed to avoid the socket going into an aborted state.
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                        throw;
                    }
                }
            }

            Func<int, Task> TickHandler(Guid guid, JsonRpc jsonRpc)
            {
                return async tick =>
                {
                    Console.WriteLine($"  Tick {guid} - #{tick}");

                    int i = Random.Shared.Next(0, 10);
                    int j = Random.Shared.Next(0, 10);

                    int result = await jsonRpc.InvokeWithCancellationAsync<int>("Add", [i, j], cancellationToken);
                    Console.WriteLine($"JSON-RPC server says {i} + {j} = {result}");

                    // example async work
                    await jsonRpc.NotifyAsync("tick", new { guid, tick });
                };
            }
        }   
    }
}
