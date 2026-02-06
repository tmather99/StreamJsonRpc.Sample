using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using StreamJsonRpc.Jit.Client.Common.MouseStream;

namespace StreamJsonRpc.Jit.Client;

// Client-side implementation that receives mouse callbacks from server
public class MouseStreamListener : IMouseStreamListener
{
    private readonly Subject<CountData> _subject = new Subject<CountData>();

    public IObservable<CountData> MouseEvents => _subject;

    public Task OnNextValue(CountData e)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"        MouseStreamListener - OnNextValue: {e.Action} (X,Y) = ({e.X}, {e.Y})");
        Console.ResetColor();
        _subject.OnNext(e);
        return Task.CompletedTask;
    }

    public Task OnError(string error)
    {
        Console.WriteLine($"        MouseStreamListener - Mouse stream error: {error}");
        _subject.OnError(new Exception(error));
        return Task.CompletedTask;
    }

    public Task OnCompleted()
    {
        Console.WriteLine("         MouseStreamListener - Mouse stream completed");
        _subject.OnCompleted();
        return Task.CompletedTask;
    }

    // Example: Filter only click events
    public IDisposable CreateClickSubscription()
    {
        return MouseEvents
            .Where(e => e.Action == MouseAction.LeftClick)
            .Subscribe(
                e =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"           -> Click detected: {e.Action} (X,Y) = ({e.X}, {e.Y})");
                    Console.WriteLine($"                   TimeStamp: {e.Timestamp}");
                    Console.WriteLine($"                      Values:\n" +
                                      $"                        [{string.Join(", ", e.ValuedList)}]");
                    Console.WriteLine($"                  Dictionary:\n" +
                                      $"                        {string.Join("\n                        ",
                                          e.ValuedDictionary.Select(kv => $"{kv.Key} = {kv.Value:O}"))}");
                    Console.ResetColor();
                },
                () =>
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("           -> Mouse stream completed!");
                    Console.ResetColor();
                });
    }

    // Example: Filter only movement events
    public IDisposable CreateMovementSubscription()
    {
        return MouseEvents
            .Where(e => e.Action == MouseAction.Move)
            .Throttle(TimeSpan.FromMilliseconds(100)) // Reduce frequency
            .Subscribe(e =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"           -> Mouse moved (X,Y) = ({e.X}, {e.Y})");
                Console.ResetColor();
            });
    }
}