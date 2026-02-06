using PolyType;

namespace StreamJsonRpc.Aot.Common.UserInfoStream;

[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IUserService
{
    Task<UserInfo> ProcessUser(UserInfo input, CancellationToken ct);

    Task<IAsyncEnumerable<UserInfo>> GetList(CancellationToken ct);
}