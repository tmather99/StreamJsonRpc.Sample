using System.IO.Pipes;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

// StreamJsonRpc client-side implementation
internal partial class Client
{
    public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
    {
        JsonRpc jsonRpc = null!;

        NumberStreamListener numberStreamListener = null!;
        MouseStreamListener mouseStreamListener = null!;

        try
        {
            // Create the MessagePack handler over the pipe
            jsonRpc = new(MessagePackHandler.Create(pipe));

            // Register client callback handlers for server push notifications
            RegisterTickHandler(jsonRpc, guid);

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register user service RPC methods
            IUserService userService = jsonRpc.Attach<IUserService>();

            // Start listening for messages
            jsonRpc.StartListening();

            // Run the main client logic
            await RunAsync();

            async Task RunAsync()
            {
                // Run the main client logic
                Console.WriteLine($"  ClientId: {guid}");
                Program.isConnected = await server.ConnectAsync(guid);

                if (Program.isConnected)
                {
                    // Test various data type marshaling
                    await Check_DataType_Marshaling(server, cts);

                    // Test custom data type marshaling
                    await Check_Custom_DataType_Marshaling(userService, cts);

                    // Start server hearbeat ticks
                    await jsonRpc.NotifyAsync("SendTicksAsync", guid);
                    Console.WriteLine($"  SendTicksAsync {guid}");

                    // Start subscription to server stream
                    numberStreamListener = await SubscribeToNumberDataStream(jsonRpc);

                    // Start subscription to server stream with client callback handlers
                    mouseStreamListener = await SubcribeToMouseDataStream(jsonRpc);

                    // blocks until canceled via Ctrl+C.
                    await jsonRpc.Completion.WithCancellation(cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                // stop heartbeat ticks
                await jsonRpc.InvokeAsync("CancelTickOperation", guid);

                // stop all stream listeners
                await numberStreamListener.Unsubscribe();
                await mouseStreamListener.Unsubscribe();
            }
            catch (Exception)
            {
                return;  // exit cleanly
            }

            throw;  // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}