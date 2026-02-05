using System.Runtime.CompilerServices;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

// Server async enumerable methods implementation
public partial class Server : IServer
{
    // IAsyncEnumerable<T> marshaling
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

    public Task<IAsyncEnumerable<int>> ProcessAsyncEnumerable(IStreamListener<int> progress, CancellationToken ct)
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