using System;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Number Stream functionality
public partial class Server
{
    // Server streams data to client using notifications
    public async Task SubscribeToNumberStream()
    {
        Console.WriteLine("  Client subscribed to number stream");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        _subscription = _subject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(int value)
        {
            try
            {
                Console.WriteLine($"      {value, 3} -> {guid}");

                if (isCancel) return;

                // Call back to client using notification
                await _numberStreamStreamListener.OnNextValue(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to client: {ex.Message}");
            }
        }

        async void OnError(Exception error)
        {
            if (isCancel) return;

            Console.WriteLine($"Stream error: {error.Message}");
            await _numberStreamStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            if (isCancel) return;

            Console.WriteLine("Stream completed");
            await _numberStreamStreamListener.OnCompleted();
        }
    }
}