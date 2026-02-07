using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PolyType;

namespace StreamJsonRpc.Jit.Client.Common.UserInfoStream;

[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IUserService
{
    Task<UserInfo> ProcessUser(UserInfo input, CancellationToken ct);

    //
    // Failed to deserializing JSON-RPC customer type!!!!!
    //
    Task<IAsyncEnumerable<UserInfo>> GetList(CancellationToken ct);
}