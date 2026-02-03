using System;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Mouse Stream functionality
public partial class Server
{
    // Static subject to aggregate mouse events from global capture service
    private static readonly Subject<MouseEventData> _globalMouseSubject = new();
    
    private IDisposable _mouseSubscription = null!;
    private readonly Subject<MouseEventData> _mouseSubject = new();

    // Static method called by the global mouse capture service
    public static void PublishMouseEventGlobal(int x, int y, MouseAction action)
    {
        var mouseEvent = new MouseEventData
        {
            X = x,
            Y = y,
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        _globalMouseSubject.OnNext(mouseEvent);
    }

    // Server streams mouse data to client using notifications
    public async Task SubscribeToMouseStream()
    {
        Console.WriteLine("  Client subscribed to mouse stream");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        // Subscribe to the global mouse subject
        _mouseSubscription = _globalMouseSubject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(MouseEventData mouseEvent)
        {
            try
            {
                if (isCancel) return;

                Console.WriteLine($"      Mouse {mouseEvent.Action} (X,Y) = ({mouseEvent.X}, {mouseEvent.Y}) -> {guid}");

                // Call back to client using notification
                await _mouseStreamListener.OnNextValue(mouseEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending mouse event to client: {ex.Message}");
            }
        }

        async void OnError(Exception error)
        {
            if (isCancel) return;

            Console.WriteLine($"Mouse stream error: {error.Message}");
            await _mouseStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            if (isCancel) return;

            Console.WriteLine("Mouse stream completed");
            await _mouseStreamListener.OnCompleted();
        }
    }

    // Simulate mouse events (call this method to publish mouse events)
    public void PublishMouseEvent(int x, int y, MouseAction action)
    {
        var mouseEvent = new MouseEventData
        {
            X = x,
            Y = y,
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        _mouseSubject.OnNext(mouseEvent);
    }

    // Unsubscribe from mouse stream
    public Task UnsubscribeFromMouseStream()
    {
        _mouseSubscription?.Dispose();
        _mouseSubscription = null!;
        Console.WriteLine("  Client unsubscribed from mouse stream");
        return Task.CompletedTask;
    }
}