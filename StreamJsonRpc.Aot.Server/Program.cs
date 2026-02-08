using System.IO.Pipes;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Server;
using StreamJsonRpc.Aot.Server.Mouse;
using StreamJsonRpc.Aot.Server.MouseStream;

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
                await Server.RunAsync(stream, ++clientRequests);
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
            _mouseCaptureService = new MouseCaptureService(MouseDataStream.PublishMouseEventGlobal);

            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("MouseDataStream is connected to global mouse service.");
                    await _mouseCaptureService.StartAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start mouse capture: {ex.Message}");
                }
            });
        }
    }
}