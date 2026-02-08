using System;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client.Common;

// Shared interface between client and server
public partial interface IDataStream
{
    Task Subscribe(Guid clientGuid);
    Task Unsubscribe(Guid clientGuid);
}