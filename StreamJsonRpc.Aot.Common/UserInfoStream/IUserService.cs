using PolyType;

namespace StreamJsonRpc.Aot.Common;

[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IUserService
{
    Task<UserInfo> ProcessUser(UserInfo input, CancellationToken ct);

    //
    // Failed to deserializing JSON-RPC customer type!!!!!
    //
    Task<IAsyncEnumerable<UserInfo>> GetList(CancellationToken ct);
}