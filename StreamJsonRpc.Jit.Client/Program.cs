using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc.Jit.Client;

class Program
{
    static public bool isConnected = false;
    static public Guid guid = Guid.NewGuid();
    static public Random rand = new Random();

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
