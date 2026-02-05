using System.Reactive.Linq;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server observer methods implementation
public partial class Server : IServer
{
    private readonly List<IObserver<int>> observers = [];

    // IObserver<T> marshaling
    public Task SetObserver(IObserver<int> observer, CancellationToken ct)
    {
        Console.WriteLine("  Subscribe");

        lock (this.observers)
        {
            this.observers.Add(observer);
        }

        Observable.Interval(TimeSpan.FromMilliseconds(300))
                .Subscribe(i =>
                {
                    if (isCancel) return;
                    ct.ThrowIfCancellationRequested();
                    int value = (int)i;   // sequential: 0,1,2,3,...
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine($"    CounterObserver - OnNext: {value}");
                    Console.ResetColor();
                    observer.OnNext(value);
                });

        return Task.CompletedTask;
    }

    public Task<IObserver<int>> GetObserver(CancellationToken ct)
    {
        Console.WriteLine("  GetObservable");
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(observers.First());
    }
}