using System;
using System.Reactive.Linq;
using PolyType;
using StreamJsonRpc;

[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IRpcService
{
    // Client sends observer → server pushes into it
    void SubscribeCounter(IObserver<int> observer);
}
