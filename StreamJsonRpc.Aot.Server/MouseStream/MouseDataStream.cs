using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

public partial class MouseDataStream : IMouseDataStream
{
    private Guid Id;

    // Static subject to aggregate mouse events from global capture service
    private static readonly Subject<MouseEventData> _globalMouseSubject = new();

    // Change to Dictionary to support multiple subscriptions
    private static readonly IDictionary<Guid, IDisposable> _mouseSubscriptions = new Dictionary<Guid, IDisposable>();
    private readonly Subject<MouseEventData> _mouseSubject = new();

    private JsonRpc _jsonRpc;

    private static readonly IDictionary<Guid, IMouseStreamListener> _mouseStreamListeners = new Dictionary<Guid, IMouseStreamListener>();

    // Static method called by the global mouse capture service
    public static void PublishMouseEventGlobal(int x, int y, MouseAction action)
    {
        if (!_mouseStreamListeners.Any())
        {
            return;
        }

        var mouseEvent = new MouseEventData {
            X = x,
            Y = y,
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        _globalMouseSubject.OnNext(mouseEvent);
    }

    public MouseDataStream(JsonRpc jsonRpc)
    {
        _jsonRpc = jsonRpc;

        jsonRpc.Disconnected += static async delegate (object? o, JsonRpcDisconnectedEventArgs e) {
            Console.WriteLine("\nMouseEventData - RPC connection closed");
            Console.WriteLine($"  Reason: {e.Reason}");
            Console.WriteLine($"  Description: {e.Description}");
            if (e.Exception != null)
                Console.WriteLine($"  Exception: {e.Exception}");
        };
    }

    public Task Subscribe(Guid clientGuid)
    {
        this.Id = clientGuid;

        Console.WriteLine($"  Client subscribed to mouse data stream {clientGuid}");

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        // Check if already subscribed
        if (_mouseSubscriptions.ContainsKey(clientGuid))
        {
            Console.WriteLine($"  Client {clientGuid} already subscribed, disposing old subscription");
            _mouseSubscriptions[clientGuid].Dispose();
        }

        _jsonRpc.AllowModificationWhileListening = true;
        IMouseStreamListener mouseStreamListener = _jsonRpc.Attach<IMouseStreamListener>();
        _jsonRpc.AllowModificationWhileListening = false;

        _mouseStreamListeners[clientGuid] = mouseStreamListener;

        // Subscribe to the global mouse subject - store per clientGuid
        _mouseSubscriptions[clientGuid] = _globalMouseSubject.Subscribe(OnNext, OnError, OnCompleted);

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
                await _mouseStreamListeners[clientGuid].OnNextValue(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending mouse event to client: {ex.Message}");
            }
        }

        async void OnError(Exception error)
        {
            Console.WriteLine($"Mouse stream error: {error.Message}");
            await _mouseStreamListeners[clientGuid].OnError(error.Message);
        }

        async void OnCompleted()
        {
            Console.WriteLine("Mouse stream completed");
            await _mouseStreamListeners[clientGuid].OnCompleted();
        }

        return Task.CompletedTask;
    }

    public Task Unsubscribe(Guid clientGuid)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Unsubscribe client {clientGuid} from data stream.");
        Console.ResetColor();

        if (_mouseSubscriptions.TryGetValue(clientGuid, out IDisposable? subscription))
        {
            subscription.Dispose();
            _mouseSubscriptions.Remove(clientGuid);
        }

        if (_mouseStreamListeners.ContainsKey(clientGuid))
        {
            _mouseStreamListeners.Remove(clientGuid);
        }

        return Task.CompletedTask;
    }
}