using System;
using System.Diagnostics.Metrics;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace StreamJsonRpc.Aot.Server;

// Server implementation
internal class Server : IServer
{
    private bool isCancel;
    private int tickNumber = 0;
    private IDisposable? _subscription = null;
    private JsonRpc? jsonRpc = null;

    private readonly Subject<int> _subject = new();

    public Server()
    {
        // Simulate publishing data periodically
        Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Subscribe(i =>
            {
                int r = Random.Shared.Next(1, 100);
                _subject.OnNext(r);
            });
    }

    public void SetClientRpc(JsonRpc jsonRpc)
    {
        this.jsonRpc = jsonRpc;
    }

    public Task<bool> ConnectAsync(Guid guid)
    {
        Console.WriteLine($"  ClientId: {guid}");
        return Task.FromResult(true);
    }

    public Task CancelTickOperation(Guid guid)
    {
        isCancel = true;
        Console.WriteLine($"Cancel Tick Operation for {guid}");
        return Task.CompletedTask;
    }

    public async Task SendTicksAsync(Guid guid)
    {
        Console.WriteLine($"  SendTicksAsync isCancel={isCancel}");

        if (jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        while (!isCancel)
        {
            await jsonRpc.NotifyAsync("Tick", ++tickNumber);
            Console.WriteLine($"    Notify clientId {guid} - #{tickNumber}");
            await Task.Delay(1000);
        }

        _subscription?.Dispose();
        _subscription = null;
    }

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

    // Server streams data to client using notifications
    public async Task SubscribeToNumberStream()
    {
        Console.WriteLine("  Client subscribed to number stream");

        if (jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        _subscription = _subject.Subscribe(
            async value =>
            {
                try
                {
                    Console.WriteLine($"     -> {value}, isCancel={isCancel}");

                    if (isCancel) return;

                    // Call back to client using notification
                    await jsonRpc.NotifyAsync("OnNextValue", value);
                    //await this.listner.OnNextValue(value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending to client: {ex.Message}");
                }
            },
            async error =>
            {
                if (isCancel) return;

                Console.WriteLine($"Stream error: {error.Message}");
                await jsonRpc.NotifyAsync("OnError", error.Message);
            },
            async () =>
            {
                if (isCancel) return;

                Console.WriteLine("Stream completed");
                await jsonRpc.NotifyAsync("OnCompleted");
            });
    }
}