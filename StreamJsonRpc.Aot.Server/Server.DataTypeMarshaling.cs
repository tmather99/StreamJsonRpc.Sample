using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server data methods implementation
public partial class Server : IServer
{
    public Task<int> AddAsync(int a, int b)
    {
        int sum = a + b;
        Console.WriteLine($"  Calculating: {a} + {b} = {sum}");
        return Task.FromResult(sum);
    }

    Task<List<string>> IServer.GetListAsync()
    {
        Console.WriteLine("  GetListAsync.");
        List<string> list = new List<string>
        {
            "alpha",
            "beta",
            "gamma",
            DateTime.UtcNow.ToString("O"), // ISO 8601 round-trip format
        };

        return Task.FromResult(list);
    }

    Task<Dictionary<Guid, DateTime>> IServer.GetDictionaryAsync()
    {
        Console.WriteLine("  GetDictionaryAsync.");
        Dictionary<Guid, DateTime> dict = new Dictionary<Guid, DateTime> {
            [Guid.NewGuid()] = DateTime.UtcNow,
            [Guid.NewGuid()] = DateTime.UtcNow.AddMinutes(-5),
            [Guid.NewGuid()] = DateTime.UtcNow.AddDays(1),
        };

        return Task.FromResult(dict);
    }

    public Task<Dictionary<string, string>> GetTableAsync()
    {
        Console.WriteLine("  GetTableAsync.");
        var table = new Dictionary<string, string> {
            ["Name"] = "Alice",
            ["Role"] = "Tester",
            ["Environment"] = "Dev",
            ["Timestamp"] = DateTime.UtcNow.ToString("O") // ISO 8601 for consistency
        };

        return Task.FromResult(table);
    }

    public Task SetObserver(IObserver<int> observer, CancellationToken ct)
    {
        Console.WriteLine("  Subscribe");

        lock (this.observers)
        {
            this.observers.Add(observer);

            Observable.Interval(TimeSpan.FromMilliseconds(300))
                .Subscribe(i =>
                {
                    if (isCancel) return;
                    ct.ThrowIfCancellationRequested();
                    int r = Random.Shared.Next(1, 100);
                    Console.WriteLine($"  CounterObserver - OnNext: {r}");
                    observer.OnNext(r);
                });
        }

        return Task.CompletedTask;
    }

    public Task<IObserver<int>> GetObserver(CancellationToken ct)
    {
        Console.WriteLine("  GetObservable");
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(observers.First());
    }
    
    public async Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);

        Console.WriteLine("  SetAsyncEnumerable.");

        await foreach (int value in values.WithCancellation(ct))
        {
            Console.WriteLine($"    Received value: {value}");
        }
    }

    public Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken ct)
    {
        Console.WriteLine("  GetAsyncEnumerable.");

        IAsyncEnumerable<int> Stream()
        {
            return GetValues(ct);
        }

        return Task.FromResult(Stream());

        async IAsyncEnumerable<int> GetValues([EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 1; i <= 20; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return i;
            }
        }
    }
}