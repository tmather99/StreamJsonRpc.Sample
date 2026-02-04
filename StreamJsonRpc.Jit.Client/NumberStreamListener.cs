using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client;

// Client-side implementation that receives callbacks from server
public class NumberStreamListener : INumberStreamListener
{
    private readonly Subject<int> _subject = new Subject<int>();

    public IObservable<int> Values => _subject;

    public Task OnNextValue(int value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"        NumberStreamListener - OnNextValue: {value}");
        Console.ResetColor();
        _subject.OnNext(value);
        return Task.CompletedTask;
    }

    public Task OnError(string error)
    {
        Console.WriteLine($"        NumberStreamListener - Stream error: {error}");
        _subject.OnError(new Exception(error));
        return Task.CompletedTask;
    }

    public Task OnCompleted()
    {
        Console.WriteLine("         NumberStreamListener - Stream completed");
        _subject.OnCompleted();
        return Task.CompletedTask;
    }

    public IDisposable CreateFilteredSubscription()
    {
        // Apply Rx operators to the observable
        return Values
            .Where(x => x % 2 == 0)
            .Take(5)  // Take only first 5 values
            .Subscribe(
                x =>
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"           -> Even number filter: {x}");
                    Console.ResetColor();
                },
                () =>
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("           -> !!! Completed !!!");
                    Console.ResetColor();
                });
    }
}