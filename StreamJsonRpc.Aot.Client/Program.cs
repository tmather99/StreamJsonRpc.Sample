using System.IO.Pipes;

namespace StreamJsonRpc.Aot.Client;

internal class Program
{
    internal static bool isConnected = false;
    static Guid guid = Guid.NewGuid();

    // Setup connection and handle ctrl+c to cancel the client.
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
