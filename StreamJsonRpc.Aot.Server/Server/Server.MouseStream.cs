using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;
using StreamJsonRpc.Aot.Common.MouseStream;

namespace StreamJsonRpc.Aot.Server;

// Server implementation - Mouse Stream functionality
public partial class Server
{
    private IMouseStreamListener _mouseStreamListener = null!;

    // Static subject to aggregate mouse events from global capture service
    private static readonly Subject<MouseEventData> _globalMouseSubject = new();

    private IDisposable _mouseSubscription = null!;
    private readonly Subject<MouseEventData> _mouseSubject = new();

    // Static method called by the global mouse capture service
    public static void PublishMouseEventGlobal(int x, int y, MouseAction action)
    {
        var mouseEvent = new MouseEventData {
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

        _jsonRpc.AllowModificationWhileListening = true;
        _mouseStreamListener = _jsonRpc.Attach<IMouseStreamListener>();
        _jsonRpc.AllowModificationWhileListening = false;

        // Subscribe to the global mouse subject
        _mouseSubscription = _globalMouseSubject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(MouseEventData e)
        {
            try
            {
                if (isCancel) return;

                if (e.Action == MouseAction.LeftClick)
                {
                    e.ValuedList = [
                        Guid.NewGuid().ToString(),
                        Guid.NewGuid().ToString()
                    ];

                    e.ValuedDictionary = new Dictionary<Guid, string> {
                        [Guid.NewGuid()] = $"{Guid.NewGuid()}",
                        [Guid.NewGuid()] = $"{Guid.NewGuid()}",
                    };

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"          -> Click detected: {e.Action} (X,Y) = ({e.X}, {e.Y})");
                    Console.WriteLine($"                  TimeStamp: {e.Timestamp}");
                    Console.WriteLine($"                     Values:\n" +
                                      $"                       [{string.Join(", ", e.ValuedList)}]");
                    Console.WriteLine($"                 Dictionary:\n" +
                                      $"                       {string.Join("\n                       ",
                                          e.ValuedDictionary.Select(kv => $"{kv.Key} = {kv.Value:O}"))}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      Mouse {e.Action} (X,Y) = ({e.X}, {e.Y}) -> {clientGuid}");
                    Console.ResetColor();
                }

                // Call back to client using notification
                await _mouseStreamListener.OnNextValue(e);
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
        var mouseEvent = new MouseEventData {
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