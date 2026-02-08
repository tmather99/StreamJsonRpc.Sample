using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

public partial class UserService : IUserService
{
    // JSON-RPC connection to client
    private readonly JsonRpc _jsonRpc;

    // RPC session to client
    public UserService(JsonRpc jsonRpc)
    {
        _jsonRpc = jsonRpc;
    }

    public Task<UserInfo> ProcessUser(UserInfo input, CancellationToken ct)
    {
        Console.WriteLine("  ProcessUser");

        Console.WriteLine($"    -> SERVER received: {input.Name}, {input.Age}");

        return Task.FromResult(new UserInfo {
            Name = input.Name.ToUpper(),
            Age = input.Age + 1
        });
    }

    public Task<IAsyncEnumerable<UserInfo>> GetList(CancellationToken ct)
    {
        Console.WriteLine("  GetList.");
        List<UserInfo> list =
        [
            new UserInfo { Name = "alpha", Age = 20 },
            new UserInfo { Name = "beta", Age = 30 },
            new UserInfo { Name = "gamma", Age = 40 },
            new UserInfo { Name = "delta", Age = 50 }
        ];

        async IAsyncEnumerable<UserInfo> GetUserInfos()
        {
            foreach (var user in list)
            {
                yield return user;
                await Task.Yield();
            }
        }

        return Task.FromResult<IAsyncEnumerable<UserInfo>>(GetUserInfos());
    }
}