using PolyType;

namespace StreamJsonRpc.Aot.Common;

// Concrete interface for mouse events
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface INumberDataStream : IDataStream
{
    //
    // NOTE: Can not use interface inheritance for data stream contract.
    //
    //   Task Subscribe(Guid clientGuid);
    //   Task Unsubscribe(Guid clientGuid);

    Task SubscribeToNumberStream(Guid clientGuid);
    Task UnsubscribeFromNumberStream(Guid clientGuid);
}