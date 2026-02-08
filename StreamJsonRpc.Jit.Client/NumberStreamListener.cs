using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using StreamJsonRpc.Jit.Client.Common;

namespace StreamJsonRpc.Jit.Client;

// Client-side implementation that receives callbacks from server
public class NumberStreamListener : INumberStreamListener
{
    public readonly Guid Id = Guid.NewGuid();

    private readonly Subject<int> _subject = new Subject<int>();

    public IObservable<int> Values => _subject;

    private INumberDataStream _numberDataStream;

    private readonly IDisposable? numberSubscription = null;

    public NumberStreamListener(INumberDataStream numberDataStream)
    {
        _numberDataStream = numberDataStream;
        _subject = new Subject<int>();

        // Subscribe to filtered number stream
        this.numberSubscription = this.CreateFilteredSubscription();
    }

    public Task Subscribe()
    {
        return _numberDataStream.Subscribe(Id);
    }

    public Task Unsubscribe()
    {
        this.numberSubscription?.Dispose();
        return _numberDataStream.Unsubscribe(Id);
    }

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