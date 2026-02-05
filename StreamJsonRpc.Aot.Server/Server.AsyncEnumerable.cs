using System.Runtime.CompilerServices;
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

    // IAsyncEnumerable<T> marshaling - server pushes data to client with progress using async enumerable
    public Task<IAsyncEnumerable<int>> ProcessAsyncEnumerable(IObserver<int> progress, CancellationToken ct)
    {
        Console.WriteLine("  ProcessAsyncEnumerable.");
        
        return Task.FromResult(Generate());

        async IAsyncEnumerable<int> Generate([EnumeratorCancellation] CancellationToken token = default)
        {
            for (int i = 1; i <= 10; i++)
            {
                token.ThrowIfCancellationRequested();

                // Simulate work
                await Task.Delay(300, token);

                // 🔹 Progress callback (push to client immediately)
                progress?.OnNext(i * 10); // e.g., % progress

                // 🔹 Streamed result value
                Console.WriteLine($"Server yielding: {i}");
                yield return i;
            }

            progress?.OnCompleted();
        }
    }

    // May not needed, but just to test duplex streaming with client pushing progress updates using IAsyncEnumerable
    public Task<IAsyncEnumerable<int>> DuplexAsyncEnumerable(IAsyncEnumerable<int> clientStream, CancellationToken ct)
    {
        Console.WriteLine("ProcessAsyncEnumerable (duplex streaming) invoked.");

        return Task.FromResult(Generate());

        async IAsyncEnumerable<int> Generate([EnumeratorCancellation] CancellationToken token = default)
        {
            int lastProgress = 0;

            // Read client progress in background
            var serverTask = Task.Run(async () =>
            {
                await foreach (int p in clientStream.WithCancellation(token))
                {
                    Console.WriteLine($"Server received progress from client: {p}");
                    lastProgress = p;
                }
            }, token);

            // Meanwhile server produces its own stream
            for (int i = 1; i <= 10; i++)
            {
                token.ThrowIfCancellationRequested();

                await Task.Delay(400, token);

                int result = i * 100 + lastProgress; // mix both sides just for testing
                Console.WriteLine($"Server yielding result: {result}");

                yield return result;
            }

            await serverTask; // ensure we consumed client stream
        }
    }
}