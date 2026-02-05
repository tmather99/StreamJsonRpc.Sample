using System;
using System.Runtime.Serialization;
using PolyType;

namespace StreamJsonRpc.Jit.Client;

// Concrete interface
[JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ICounterObserver : IObserver<int>
{
}