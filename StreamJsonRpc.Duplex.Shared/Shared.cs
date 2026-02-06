using System.Collections.Generic;
using System.Threading;
using StreamJsonRpc;
using PolyType;

namespace StreamJsonRpc.IPC.Shared
{
    /// <summary>
    /// Shared RPC contract interface for duplex streaming
    /// </summary>
    [JsonRpcContract]
    [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    public partial interface IRpcService
    {
        /// <summary>
        /// Send stream to server (one direction)
        /// </summary>
        [JsonRpcMethod("sendStream")]
        Task SendStreamAsync(IAsyncEnumerable<int> clientStream, CancellationToken ct = default);
        
        /// <summary>
        /// Get stream from server (other direction)
        /// </summary>
        [JsonRpcMethod("getStream")]
        IAsyncEnumerable<int> GetStreamAsync(int[] inputValues, CancellationToken ct = default);
    }
}