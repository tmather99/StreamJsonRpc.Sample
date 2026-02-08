using System;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using StreamJsonRpc.Jit.Client.Common.UserInfoStream;

namespace StreamJsonRpc.Jit.Client;

internal partial class Client
{
    public static async Task RunAsync(NamedPipeClientStream pipe, Guid guid, CancellationTokenSource cts)
    {
        JsonRpc jsonRpc = null!;

        MouseStreamListener mouseStreamListener = null!;

        try
        {
            // Create the MessagePack handler over the pipe
            jsonRpc = new(MessagePackHandler.Create(pipe));

            RegisterTickHandler(jsonRpc, guid);

            // Register server RPC methods
            IServer server = jsonRpc.Attach<IServer>();

            // Register user service RPC methods
            IUserService userService = jsonRpc.Attach<IUserService>();

            // Start listening for messages
            jsonRpc.StartListening();

            // Run the main client logic
            await RunAsync();

            // Main client logic
            async Task RunAsync()
            {
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
                    await SubscribeToNumberDataStream(jsonRpc, server);

                    // Start subscription to server stream with client callback handlers
                    mouseStreamListener = await SubcribeToMouseDataStream(jsonRpc);

                    // blocks until canceled via Ctrl+C.
                    await jsonRpc.Completion.WithCancellation(cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stop hearbeat ticks
            await jsonRpc.InvokeAsync("CancelTickOperation", guid);

            await mouseStreamListener.Unsubscribe();

            throw; // rethrow to main
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }

    }
}
