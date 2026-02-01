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
}