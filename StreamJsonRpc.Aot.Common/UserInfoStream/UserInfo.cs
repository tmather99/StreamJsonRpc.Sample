using MessagePack;

namespace StreamJsonRpc.Aot.Common.UserInfoStream;

[MessagePackObject]
public partial class UserInfo
{
    [Key(0)]
    public required string Name { get; set; }

    [Key(1)]
    public int Age { get; set; }
}