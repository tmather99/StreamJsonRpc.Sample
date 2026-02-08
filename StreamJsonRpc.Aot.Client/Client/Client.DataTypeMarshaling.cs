using System.Reactive.Linq;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

// Client data type marshaling tests
internal partial class Client
{
    // StreamJsonRpc object marshaling
    private static async Task Check_DataType_Marshaling(IServer server, CancellationTokenSource cts)
    {
        // int marshaling
        int a = Random.Shared.Next(0, 10);
        int b = Random.Shared.Next(0, 10);
        int sum = await server.AddAsync(a, b);
        Console.WriteLine($"  Calculating {a} + {b} = {sum}");

        // List<string> marshaling
        List<string> list = await server.GetListAsync();
        Console.WriteLine($"  GetList:");
        Console.WriteLine(string.Join(Environment.NewLine, list.Select((v, i) => $"    [{i}] {v}")));

        // Dictionary<Guid, DateTime> marshaling
        Dictionary<Guid, DateTime> dict = await server.GetDictionaryAsync();
        Console.WriteLine($"  GetDictionary:");
        Console.WriteLine(string.Join(Environment.NewLine, dict.Select(kv => $"    {kv.Key}={kv.Value:O}")));

        // Dictionary<string, string> marshaling
        Dictionary<string, string> table = await server.GetTableAsync();
        Console.WriteLine($"  GetTable:");
        Console.WriteLine(string.Join(Environment.NewLine, table.Select(kv => $"    {kv.Key}={kv.Value:O}")));

        // IObserver<T> marshaling
        await Check_IObserver_Marshaling(server, cts);

        // IAsyncEnumerable<T> marshaling
        await Check_IAsyncEnumerable_Marshaling(server, cts);
    }

    // Custom data type marshaling
    private static async Task Check_Custom_DataType_Marshaling(IUserService userService, CancellationTokenSource cts)
    {
        UserInfo userInfo = new() {
            Name = "Alice",
            Age = 30
        };

        userInfo = await userService.ProcessUser(userInfo, cts.Token);
        Console.WriteLine("  ProcessUser");
        Console.WriteLine($"    -> Clienbt received: {userInfo.Name}, {userInfo.Age}");
    }
}