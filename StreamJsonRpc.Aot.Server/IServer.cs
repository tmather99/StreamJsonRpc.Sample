using PolyType;
using StreamJsonRpc;

// Shared code between client and server
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
internal partial interface IServer
{
    Task<bool> ConnectAsync(Guid guid);
    Task<int> AddAsync(int a, int b);
    Task<List<string>> GetListAsync();
    Task<Dictionary<Guid, DateTime>> GetDictionaryAsync();
    Task<Dictionary<string, string>> GetTableAsync();
}
