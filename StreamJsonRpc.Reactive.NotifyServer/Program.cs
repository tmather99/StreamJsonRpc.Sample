using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace StreamJsonRpc.Reactive.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Server starting...");
            Console.WriteLine($"StreamJsonRpc Version: 2.24.84");
            Console.WriteLine($"System.Reactive Version: 7.0.0-preview.1");
            Console.WriteLine("Using notification-based streaming");

            while (true)
            {
                using var pipe = new NamedPipeServerStream(
                    "StreamJsonRpcSample",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Console.WriteLine("\nWaiting for client connection...");
                await pipe.WaitForConnectionAsync();
                Console.WriteLine("Client connected!");

                var server = new Server();
                var jsonRpc = new JsonRpc(pipe, pipe);

                // Give server reference to JSON-RPC so it can call back to client
                server.SetClientRpc(jsonRpc);

                jsonRpc.AddLocalRpcTarget(server);
                jsonRpc.StartListening();

                await jsonRpc.Completion;
                Console.WriteLine("Client disconnected. Waiting for next connection...");
            }
        }
    }
}