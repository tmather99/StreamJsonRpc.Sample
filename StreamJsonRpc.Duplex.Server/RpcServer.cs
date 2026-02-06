using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nerdbank.Streams;
using StreamJsonRpc;
using StreamJsonRpc.IPC.Shared;

namespace StreamJsonRpc.IPC.Server
{
    /// <summary>
    /// Server implementation of the RPC service
    /// </summary>
    public class RpcServerImplementation : IRpcService
    {
        private readonly ITracer _tracer;
        private readonly Channel<int> _inputChannel;

        public RpcServerImplementation(ITracer? tracer = null)
        {
            _tracer = tracer ?? new ConsoleTracer();
            _inputChannel = Channel.CreateUnbounded<int>();
        }

        /// <summary>
        /// Receive stream from client
        /// </summary>
        public async Task SendStreamAsync(IAsyncEnumerable<int> clientStream, CancellationToken ct = default)
        {
            _tracer.TraceInformation("[Server:RPC] SendStreamAsync invoked");

            try
            {
                await foreach (var item in clientStream.WithCancellation(ct))
                {
                    _tracer.TraceInformation($"[Server] Processing: {item}");
                    await _inputChannel.Writer.WriteAsync(item, ct);
                }
                
                _tracer.TraceInformation("[Server] Input stream completed");
            }
            catch (Exception ex)
            {
                _tracer.TraceError($"[Server:SendStream] Error: {ex.Message}");
                throw;
            }
            finally
            {
                _inputChannel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Send stream to client
        /// </summary>
        public async IAsyncEnumerable<int> GetStreamAsync(int[] inputValues, CancellationToken ct = default)
        {
            _tracer.TraceInformation($"[Server:RPC] GetStreamAsync invoked with {inputValues.Length} values");

            await foreach (var item in _inputChannel.Reader.ReadAllAsync(ct))
            {
                var randInt = Random.Shared.Next();
                _tracer.TraceInformation($"[Server:Output] input stream: {item} -> output stream: {randInt}");
                yield return randInt;
            }
            
            _tracer.TraceInformation("[Server:Output] Stream completed");
        }
    }

    /// <summary>
    /// Named Pipe IPC Server Host
    /// </summary>
    public class IpcServerHost : IDisposable
    {
        private readonly string _pipeName;
        private readonly ITracer _tracer;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public IpcServerHost(string pipeName, ITracer? tracer = null)
        {
            _pipeName = pipeName;
            _tracer = tracer ?? new ConsoleTracer();
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _serverTask = RunServerAsync(_cts.Token);
            _tracer.TraceInformation($"[ServerHost] Started on pipe: {_pipeName}");
        }

        private async Task RunServerAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _tracer.TraceInformation($"[ServerHost] Waiting for client connection...");

                    var pipeServer = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(ct);
                    _tracer.TraceInformation("[ServerHost] Client connected");

                    _ = Task.Run(async () => await HandleClientAsync(pipeServer, ct), ct);
                }
            }
            catch (OperationCanceledException)
            {
                _tracer.TraceInformation("[ServerHost] Server stopped");
            }
            catch (Exception ex)
            {
                _tracer.TraceError($"[ServerHost] Error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipeStream, CancellationToken ct)
        {
            try
            {
                using (pipeStream)
                {
                    var serverImpl = new RpcServerImplementation(_tracer);
                    
                    var formatter = new MessagePackFormatter();
                    var handler = new LengthHeaderMessageHandler(pipeStream, pipeStream, formatter);
                    
                    using var rpc = new JsonRpc(handler, serverImpl);
                    rpc.AllowModificationWhileListening = true;
                    rpc.StartListening();

                    _tracer.TraceInformation("[ServerHost] RPC listening");
                    await rpc.Completion;
                    _tracer.TraceInformation("[ServerHost] RPC completed");
                }
            }
            catch (Exception ex)
            {
                _tracer.TraceError($"[ServerHost] Client handler error: {ex.Message}");
            }
        }

        public void Stop()
        {
            _tracer.TraceInformation("[ServerHost] Stopping...");
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _tracer.TraceInformation("[ServerHost] Disposed");
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