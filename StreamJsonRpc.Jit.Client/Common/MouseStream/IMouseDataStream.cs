using System;
using System.Threading.Tasks;
using PolyType;

namespace StreamJsonRpc.Jit.Client.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IMouseDataStream : IDataStream
{
    [JsonRpcMethod("IMouseDataStream.Subscribe")]
    new Task Subscribe(Guid clientGuid);

    [JsonRpcMethod("IMouseDataStream.Unsubscribe")]
    new Task Unsubscribe(Guid clientGuid);
}