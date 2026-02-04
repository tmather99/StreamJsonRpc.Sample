using System.IO.Pipes;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Common;
using StreamJsonRpc.Aot.Server;

internal class Program
{
    private static MouseCaptureService? _mouseCaptureService;

    internal static async Task Main(string[] args)
    {
        string pipeName = "Satori";
        int clientRequests = 0;

        try
        {
            // Start global mouse capture at application startup
            if (OperatingSystem.IsWindows())
            {
                StartMouseCaptureService();
            }
            else
            {
                Console.WriteLine("Mouse capture service is only supported on Windows.");
            }

            // Handle Ctrl+C for graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\nShutting down mouse capture service...");
                _mouseCaptureService?.Stop();
                _mouseCaptureService?.Dispose();
                e.Cancel = false;
            };

            while (true)
            {
                await Console.Error.WriteLineAsync($"\nWaiting on pipeName: {pipeName}...");

                NamedPipeServerStream stream = new(pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await stream.WaitForConnectionAsync();
                await RunAsync(stream, ++clientRequests);
            }
        }
        finally
        {
            // Cleanup on application exit
            Console.WriteLine("\nApplication shutting down...");
            _mouseCaptureService?.Stop();
            _mouseCaptureService?.Dispose();
        }

        // Start the mouse capture service
        static void StartMouseCaptureService()
        {
            _mouseCaptureService = new MouseCaptureService(Server.PublishMouseEventGlobal);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _mouseCaptureService.StartAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start mouse capture: {ex.Message}");
                }
            });
        }

        // Handle each client connection
        static async Task RunAsync(NamedPipeServerStream pipe, int requestId)
        {
            await Console.Error.WriteLineAsync($"  Connection request #{requestId} received.");

            // Set up JSON-RPC over the named pipe
            JsonRpc jsonRpc = new(MessagePackHandler.Create(pipe)) 
            {
                CancelLocallyInvokedMethodsWhenConnectionIsClosed = true
            };

            jsonRpc.Disconnected += static async delegate (object? o, JsonRpcDisconnectedEventArgs e)
            {
                Console.WriteLine("\nRPC connection closed");
                Console.WriteLine($"  Reason: {e.Reason}");
                Console.WriteLine($"  Description: {e.Description}");
                if (e.Exception != null)
                    Console.WriteLine($"  Exception: {e.Exception}");
            };

            RpcTargetMetadata targetMetadata = RpcTargetMetadata.FromShape<IServer>();
            Server server = new Server(jsonRpc);
            jsonRpc.AddLocalRpcTarget(targetMetadata, server, null);

            jsonRpc.StartListening();

            //await jsonRpc.Completion;
            await Console.Error.WriteLineAsync($"  Request #{requestId} terminated.");
        }
    }
    

}