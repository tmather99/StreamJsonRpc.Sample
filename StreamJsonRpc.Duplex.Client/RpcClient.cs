using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Streams;
using StreamJsonRpc;
using StreamJsonRpc.IPC.Shared;

namespace StreamJsonRpc.IPC.Client
{
    /// <summary>
    /// Named Pipe IPC Client
    /// </summary>
    public class IpcClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly ITracer _tracer;
        private NamedPipeClientStream? _pipeClient;
        private JsonRpc? _rpc;
        private IRpcService? _service;

        public IpcClient(string pipeName, ITracer? tracer = null)
        {
            _pipeName = pipeName;
            _tracer = tracer ?? new ConsoleTracer();
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            _tracer.TraceInformation($"[Client] Connecting to pipe: {_pipeName}");

            _pipeClient = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await _pipeClient.ConnectAsync(5000, ct);
            _tracer.TraceInformation("[Client] Connected");

            var formatter = new MessagePackFormatter();
            var handler = new LengthHeaderMessageHandler(_pipeClient, _pipeClient, formatter);
            _rpc = new JsonRpc(handler);

            // CRITICAL: Allow async enumerable arguments to stream
            _rpc.AllowModificationWhileListening = true;
            
            _service = _rpc.Attach<IRpcService>();
            _rpc.StartListening();

            _tracer.TraceInformation("[Client] RPC attached and listening");
        }

        /// <summary>
        /// Invoke duplex streaming using two separate RPC calls
        /// </summary>
        public async Task<int[]> InvokeDuplexStreamAsync(int[] inputData, CancellationToken ct = default)
        {
            if (_service == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            _tracer.TraceInformation($"[Client] Invoking duplex stream with {inputData.Length} items");

            // Start receiving results first
            var receiveTask = Task.Run(async () =>
            {
                var results = new List<int>();
                try
                {
                    var outputStream = _service.GetStreamAsync(inputData, ct);
                    await foreach (var item in outputStream.WithCancellation(ct))
                    {
                        _tracer.TraceInformation($"[Client] Received: {item}");
                        results.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _tracer.TraceError($"[Client] Error reading output: {ex.Message}");
                    throw;
                }
                return results.ToArray();
            }, ct);

            // Give the receiver a moment to start
            await Task.Delay(100, ct);

            // Now send the input stream
            try
            {
                var inputStream = CreateInputStreamAsync(inputData, ct);
                await _service.SendStreamAsync(inputStream, ct);
                _tracer.TraceInformation("[Client] Send completed");
            }
            catch (Exception ex)
            {
                _tracer.TraceError($"[Client] Error sending input: {ex.Message}");
                throw;
            }

            // Wait for all results
            var finalResults = await receiveTask;
            _tracer.TraceInformation($"[Client] Completed - received {finalResults.Length} items");
            return finalResults;
        }

        /// <summary>
        /// Creates an async enumerable stream from input data
        /// StreamJsonRpc will start consuming this immediately when the RPC method is invoked
        /// </summary>
        private async IAsyncEnumerable<int> CreateInputStreamAsync(int[] data, CancellationToken ct)
        {
            foreach (var item in data)
            {
                _tracer.TraceInformation($"[Client:Input] Sending: {item}");
                yield return item;

                // Optional: Add small delay to simulate streaming
                await Task.Delay(100, ct);
            }

            _tracer.TraceInformation("[Client:Input] Stream completed");
        }

        public void Dispose()
        {
            _tracer.TraceInformation("[Client] Disposing");
            _rpc?.Dispose();
            _pipeClient?.Dispose();
        }
    }

    /// <summary>
    /// Tracer interface for logging
    /// </summary>
    public interface ITracer
    {
        void TraceInformation(string message);
        void TraceError(string message);
    }

    /// <summary>
    /// Console-based tracer implementation
    /// </summary>
    public class ConsoleTracer : ITracer
    {
        public void TraceInformation(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public void TraceError(string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}");
            Console.ForegroundColor = oldColor;
        }
    }
}