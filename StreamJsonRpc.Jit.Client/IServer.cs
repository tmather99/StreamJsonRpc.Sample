using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Shared code between client and server
public interface IServer
{
    Task<bool> ConnectAsync(Guid guid);
    Task<int> AddAsync(int a, int b);
    Task<List<string>> GetListAsync();
    Task<Dictionary<Guid, DateTime>> GetDictionaryAsync();
    Task<Dictionary<string, string>> GetTableAsync();

    Task SendTicksAsync(Guid guid);
    Task CancelTickOperation(Guid guid);

    Task SubscribeToNumberStream();
}
