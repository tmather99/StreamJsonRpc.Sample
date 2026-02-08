using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server async enumerable methods implementation
public partial class Server : IServer
{
    // IAsyncEnumerable<T> marshaling - client pushes data to server using async enumerable
    public async Task SetAsyncEnumerable(IAsyncEnumerable<int> values, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);

        Console.WriteLine("  SetAsyncEnumerable.");

        await foreach (int value in values.WithCancellation(ct))
        {
            Console.WriteLine($"    Received value: {value}");
        }
    }

    // IAsyncEnumerable<T> marshaling - server pushes data to client using async enumerable
    public Task<IAsyncEnumerable<int>> GetAsyncEnumerable(CancellationToken ct)
    {
        Console.WriteLine("  GetAsyncEnumerable.");

        IAsyncEnumerable<int> Stream()
        {
            return GetValues(ct);
        }

        return Task.FromResult(Stream());

        async IAsyncEnumerable<int> GetValues(CancellationToken ct)
        {
            for (int i = 1; i <= 20; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return i;
            }
        }
    }

    // IAsyncEnumerable<T> marshaling - server pushes data to client with progress using async enumerable
    public Task<IAsyncEnumerable<int>> ProcessAsyncEnumerable(IObserver<int> progress, CancellationToken ct)
    {
        Console.WriteLine("  ProcessAsyncEnumerable.");

        return Task.FromResult(Generate());

        async IAsyncEnumerable<int> Generate(CancellationToken token = default)
        {
            for (int i = 1; i <= 10; i++)
            {
                token.ThrowIfCancellationRequested();

                // Simulate work
                await Task.Delay(100, token);

                // 🔹 Progress callback (push to client immediately)
                progress?.OnNext(i * 10); // e.g., % progress

                // 🔹 Streamed result value
                Console.WriteLine($"    -> Server yielding: {i}");
                yield return i;
            }

            // Final progress callback to indicate completion
            progress?.OnCompleted();
        }
    }

    // Duplex streaming with proper synchronization and cancellation
    public Task<IAsyncEnumerable<int>> DuplexAsyncEnumerable(IAsyncEnumerable<int> clientStream, CancellationToken ct)
    {
        Console.WriteLine("  DuplexAsyncEnumerable.");

        Console.WriteLine("SERVER: method entered");

        // 🔥 START READING CLIENT STREAM NOW
        var readClientTask = Task.Run(async () =>
        {
            await foreach (int x in clientStream.WithCancellation(ct))
            {
                Console.WriteLine($"SERVER RECEIVED: {x}");
            }
            Console.WriteLine("SERVER: client stream finished");
        }, ct);

        // Return server stream immediately
        return Task.FromResult(ServerStream(ct));
    }

    private async IAsyncEnumerable<int> ServerStream(CancellationToken ct)
    {
        for (int i = 1; i <= 5; i++)
        {
            await Task.Delay(500, ct);
            Console.WriteLine($"SERVER SENDING: {i * 100}");
            yield return i * 100;
        }

        Console.WriteLine("SERVER: outgoing stream finished");
    }

    public Task SetNumberStreamListener(INumberStreamListener listner, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}