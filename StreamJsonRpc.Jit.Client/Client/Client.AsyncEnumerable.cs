
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client;

// Client IAsyncEnumerable<T> marshaling tests
internal partial class Client
{
    // IAsyncEnumerable<T> marshaling
    private static async Task Check_IAsyncEnumerable_Marshaling(IServer server, CancellationTokenSource cts)
    {
        // Get stream from server and consume it
        IAsyncEnumerable<int> stream = await server.GetAsyncEnumerable(cts.Token);
        Console.WriteLine($"  GetAsyncEnumerable:");
        Console.WriteLine("    [" + string.Join(", ", await ToListAsync(stream, cts.Token)) + "]");

        await server.SetAsyncEnumerable(ProduceNumbers(cts.Token), cts.Token);
        Console.WriteLine($"  SetAsyncEnumerable.");

        async IAsyncEnumerable<int> ProduceNumbers(CancellationToken ct = default)
        {
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(100, ct); // simulate async work
                yield return i;
            }
        }

        // Push async stream to server and process it with client-side progress reporting
        await ProcessAsyncEnumerable(server, cts);
    }

    // Push async stream to server
    private static async Task ProcessAsyncEnumerable(IServer server, CancellationTokenSource cts)
    {
        // Process server stream with client-side progress reporting
        IAsyncEnumerable<int> valueStream = await server.ProcessAsyncEnumerable(new ProgressObserver(), cts.Token);
        Console.WriteLine($"  ProcessAsyncEnumerable.");

        await foreach (int item in valueStream.WithCancellation(cts.Token))
        {
            Console.WriteLine($"    Client received stream value: {item}");
        }

        Console.WriteLine("\n    -- Server stream completed successfully --\n");
    }

    // net48 does not have ToListAsync extension method
    private static async Task<List<int>> ToListAsync(IAsyncEnumerable<int> source, CancellationToken ct = default)
    {
        var list = new List<int>();
        await foreach (int item in source.WithCancellation(ct))
        {
            list.Add(item);
        }

        return list;
    }

    // Implementation of IObserver<int> to receive progress updates from server
    class ProgressObserver : IObserver<int>
    {
        //
        // NOTE: progress is not received for net48 client.
        //
        public void OnNext(int value) => Console.WriteLine($"      Progress: {value}%");
        public void OnCompleted() => Console.WriteLine("\n      Progress complete.");
        public void OnError(Exception error) => Console.WriteLine(error);
    }
}