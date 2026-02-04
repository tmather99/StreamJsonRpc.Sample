using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client
{
    class Program
    {
        internal static bool isConnected = false;
        internal static Guid guid = Guid.NewGuid();
        internal static Random rand = new Random();

        // Setup connection and handle ctrl+c to cancel the client.
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
                    await Client.RunAsync(stream, guid, cts);
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
    }
}
