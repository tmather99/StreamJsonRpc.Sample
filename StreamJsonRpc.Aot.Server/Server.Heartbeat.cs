using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server heartbeat/tick methods implementation
public partial class Server : IServer
{
    // Client requests to cancel tick operation
    public Task CancelTickOperation(Guid guid)
    {
        isCancel = true;
        Console.WriteLine($"Cancel Tick Operation for {guid}");
        return Task.CompletedTask;
    }

    // Server to start sending periodic tick notifications to client
    public async Task SendTicksAsync(Guid guid)
    {
        Console.WriteLine($"  SendTicksAsync isCancel={isCancel}");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        while (!isCancel)
        {
            // Send notification with tick number sequence per client
            await _jsonRpc.NotifyAsync("Tick", ++tickNumber);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Notify clientId {guid} - #{tickNumber}");
            Console.ResetColor();
            await Task.Delay(1000);
        }

        _numberStreamSubject.Dispose();
        _numberSubscription?.Dispose();
        _numberSubscription = null!;
    }
}