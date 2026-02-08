using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

public partial class MouseDataStream(Server server) : IMouseDataStream, IDisposable
{
    private Guid Id;

    // Static subject to aggregate mouse events from global capture service
    private static readonly Subject<MouseEventData> _globalMouseSubject = new();

    // Change to Dictionary to support multiple subscriptions
    private IDisposable _mouseSubscription = null!;
    private readonly Subject<MouseEventData> _mouseSubject = new();

    private IMouseStreamListener _mouseStreamListener = null!;

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

    public Task Subscribe(Guid clientGuid)
    {
        this.Id = clientGuid;

        Console.WriteLine($"  Client subscribed to mouse data stream {clientGuid}");

        if (server == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        JsonRpc jsonRpc = server.jsonRpc;
        jsonRpc.AllowModificationWhileListening = true;
        _mouseStreamListener = jsonRpc.Attach<IMouseStreamListener>();
        jsonRpc.AllowModificationWhileListening = false;

        // Subscribe to the global mouse subject - store per clientGuid
        _mouseSubscription = _globalMouseSubject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(MouseEventData e)
        {
            try
            {
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
                    Console.WriteLine($"                         Id: {clientGuid}");
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
                    Console.WriteLine($"      Mouse {e.Action} (X,Y) = ({e.X}, {e.Y})");
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
            Console.WriteLine($"Mouse stream error: {error.Message}");
            await _mouseStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            Console.WriteLine("Mouse stream completed");
            await _mouseStreamListener.OnCompleted();
        }

        return Task.CompletedTask;
    }

    public Task Unsubscribe(Guid clientGuid)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Unsubscribe client {clientGuid} from data stream.");
        Console.ResetColor();

        _mouseSubscription?.Dispose();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mouseSubscription?.Dispose();
    }
}