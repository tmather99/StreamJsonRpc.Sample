using System;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc.IPC.Server;

namespace StreamJsonRpc.IPC.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string pipeName = "StreamJsonRpc.DuplexDemo";

            Console.WriteLine("=== StreamJsonRpc IPC Server ===");
            Console.WriteLine($"Pipe Name: {pipeName}");
            Console.WriteLine("Press Ctrl+C to stop\n");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var server = new IpcServerHost(pipeName);

            server.Start();
            Console.WriteLine("Server started. Waiting for clients...\n");

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nShutting down server...");
            }
        }
    }
}