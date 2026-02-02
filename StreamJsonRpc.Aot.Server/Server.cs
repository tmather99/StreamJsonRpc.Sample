using System;

namespace StreamJsonRpc.Aot.Server;

// Server implementation
internal class Server : IServer
{
    public Task<bool> ConnectAsync(Guid guid)
    {
        Console.WriteLine($"  ClientId: {guid}");
        return Task.FromResult(true);
    }

    public Task<int> AddAsync(int a, int b)
    {
        int sum = a + b;
        Console.WriteLine($"  Calculating: {a} + {b} = {sum}");
        return Task.FromResult(sum);
    }

    Task<List<string>> IServer.GetListAsync()
    {
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
        Dictionary<Guid, DateTime> dict = new Dictionary<Guid, DateTime> {
            [Guid.NewGuid()] = DateTime.UtcNow,
            [Guid.NewGuid()] = DateTime.UtcNow.AddMinutes(-5),
            [Guid.NewGuid()] = DateTime.UtcNow.AddDays(1),
        };

        return Task.FromResult(dict);
    }

    public Task<Dictionary<string, string>> GetTableAsync()
    {
        var table = new Dictionary<string, string> {
            ["Name"] = "Alice",
            ["Role"] = "Tester",
            ["Environment"] = "Dev",
            ["Timestamp"] = DateTime.UtcNow.ToString("O") // ISO 8601 for consistency
        };

        return Task.FromResult(table);
    }
}