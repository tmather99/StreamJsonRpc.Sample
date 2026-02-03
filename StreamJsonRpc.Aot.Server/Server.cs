using System;
using System.Diagnostics.Metrics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation
public class Server(JsonRpc jsonRpc) : IServer
{
    private Guid guid;
    private bool isCancel;
    private int tickNumber = 0;

    // for cleanup when RPC request is canceled
    private IDisposable _subscription = null!;
    private readonly Subject<int> _subject = new();

    private readonly JsonRpc _jsonRpc = jsonRpc;
    private readonly INumberStreamStreamListener _numberStreamStreamListener = jsonRpc.Attach<INumberStreamStreamListener>();

    public Task<bool> ConnectAsync(Guid guid)
    {
        Console.WriteLine($"  ClientId: {guid}");

        this.guid = guid;

        // Simulate publishing data periodically
        Observable.Interval(TimeSpan.FromMilliseconds(100))
                  .Subscribe(i =>
                   {
                       if (isCancel) return;
                       int r = Random.Shared.Next(1, 100);
                       _subject.OnNext(r);
                   });

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

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        while (!isCancel)
        {
            // Send notification with tick number sequence per client
            await _jsonRpc.NotifyAsync("Tick", ++tickNumber);
            Console.WriteLine($"    Notify clientId {guid} - #{tickNumber}");
            await Task.Delay(1000);
        }

        _subject.Dispose();
        _subscription?.Dispose();
        _subscription = null!;
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

        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Client RPC not set");
        }

        _subscription = _subject.Subscribe(OnNext, OnError, OnCompleted);

        async void OnNext(int value)
        {
            try
            {
                Console.WriteLine($"      {value, 3} -> {guid}");

                if (isCancel) return;

                // Call back to client using notification
                await _numberStreamStreamListener.OnNextValue(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to client: {ex.Message}");
            }
        }

        async void OnError(Exception error)
        {
            if (isCancel) return;

            Console.WriteLine($"Stream error: {error.Message}");
            await _numberStreamStreamListener.OnError(error.Message);
        }

        async void OnCompleted()
        {
            if (isCancel) return;

            Console.WriteLine("Stream completed");
            await _numberStreamStreamListener.OnCompleted();
        }
    }
}