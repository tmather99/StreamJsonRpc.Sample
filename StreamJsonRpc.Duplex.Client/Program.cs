using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc.IPC.Client;

namespace StreamJsonRpc.IPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string pipeName = "StreamJsonRpc.DuplexDemo";

            Console.WriteLine("=== StreamJsonRpc IPC Client ===");
            Console.WriteLine($"Pipe Name: {pipeName}\n");

            using var client = new IpcClient(pipeName);

            try
            {
                // Connect to server
                Console.WriteLine("Connecting to server...");
                await client.ConnectAsync();
                Console.WriteLine("Connected!\n");

                // Test data
                var inputData = new[] { 1, 2, 3, 4, 5 };

                Console.WriteLine($"Sending data: [{string.Join(", ", inputData)}]");
                Console.WriteLine();

                // Invoke duplex stream
                var results = await client.InvokeDuplexStreamAsync(inputData);

                // Display results
                Console.WriteLine();
                Console.WriteLine($"Results: [{string.Join(", ", results)}]");
                Console.WriteLine($"Expected: [{string.Join(", ", inputData.Select(x => x * 2))}]");

                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}