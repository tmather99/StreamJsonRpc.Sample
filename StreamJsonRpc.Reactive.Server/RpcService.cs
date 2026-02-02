using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace StreamJsonRpc.Reactive.Server;

class RpcService : IRpcService
{
    private readonly CancellationTokenSource disposalSource = new CancellationTokenSource();
    private readonly List<IObserver<int>> observers = new List<IObserver<int>>();

    void IDisposable.Dispose() => this.disposalSource.Cancel();

    public Task<bool> GetBool()
    {
        Console.WriteLine("  GetBool");

        return Task.FromResult(true);
    }

    public Task<IAsyncEnumerable<int>> GetAsyncEnumerable()
    {
        Console.WriteLine("  GetAsyncEnumerable");

        return Task.FromResult(Generate());

        static async IAsyncEnumerable<int> Generate()
        {
            yield return 10;
            yield return 20;
            yield return 30;
            await Task.CompletedTask;
        }
    }

    public Task Subcribe(IObserver<int> observer)
    {
        Console.WriteLine("  Subscribe");

        lock (this.observers)
        {
            this.observers.Add(observer);
        }

        return Task.CompletedTask;
    }

    public Task<IObservable<int>> GetObservable()
    {
        Console.WriteLine("  GetObservable");

        // Return an observable that clients can subscribe to
        return Task.FromResult(Observable.Interval(TimeSpan.FromSeconds(1))
            .Select(i => (int)i));
    }
}
