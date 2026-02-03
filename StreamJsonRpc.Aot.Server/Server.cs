using System;
using System.Diagnostics.Metrics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server implementation
public partial class Server(JsonRpc jsonRpc) : IServer
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
}