using System;
using System.Threading.Tasks;
using PolyType;

namespace StreamJsonRpc.Jit.Client.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberDataStream
{
    //
    // NOTE: Can not use interface inheritance for data stream contract.
    //
    //   Task Subscribe(Guid clientGuid);
    //   Task Unsubscribe(Guid clientGuid);

    Task SubscribeToNumberStream(Guid clientGuid);
    Task UnsubscribeFromNumberStream(Guid clientGuid);
}