using System.Reactive.Linq;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server observer methods implementation
public partial class Server : IServer
{
    private readonly List<IObserver<int>> observers = [];

    // IObserver<T> marshaling for AOT client
    public Task SetObserver(IObserver<int> observer, CancellationToken ct)
    {
        Console.WriteLine("  SetObserver");

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

    // IObserver<T> marshaling with server callback for net48 client
    public Task SetCounterObserver(IObserver<int> observer, Guid oid, CancellationToken ct)
    {
        Console.WriteLine("  SetCounterObserver");

        lock (this.observers)
        {
            this.observers.Add(observer);
        }

        _ = this.SubscribeToCounterObserverStream(observer, oid);

        return Task.CompletedTask;
    }

    // IObserver<T> marshaling - server pushes data to client using observer callback
    public Task<IObserver<int>> GetObserver(CancellationToken ct)
    {
        Console.WriteLine("  GetObservable");
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(observers.First());
    }
}