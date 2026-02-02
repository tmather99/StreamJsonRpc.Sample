# StreamJsonRpc with IObservable<T> and IObserver<T> Sample

This sample demonstrates how to use StreamJsonRpc 2.24.84 with System.Reactive for streaming data between client and server.

## Architecture

Since StreamJsonRpc 2.24.84 doesn't support direct IObservable/IObserver marshaling with the default JSON formatter,
this sample uses a **notification-based callback pattern**:

1. **Server streams to client**: Server calls `jsonRpc.NotifyAsync()` to push values to client
2. **Client exposes IObservable**: Client wraps notifications in a Subject<T> to create an observable stream
3. **Rx operators work**: Client can apply any Rx operator to the observable

## Key Components

### Server
- `Server.cs`: Contains the streaming logic with Observable.Interval
- Calls back to client using `NotifyAsync("OnNextValue", value)`
- Supports cancellation via CancellationToken

### Client
- `ClientCallbacks.cs`: Receives notifications and exposes IObservable<int>
- Registered with `jsonRpc.AddLocalRpcTarget(callbacks)`
- Client can apply Rx operators: Where, Select, Buffer, etc.

## Running the Sample
```bash
# Terminal 1 - Start server
dotnet run --project Server

# Terminal 2 - Start client
dotnet run --project Client
```

## Examples Demonstrated

1. **Basic streaming**: Server streams values, client receives them
2. **Filtered subscription**: Client uses Rx Where operator
3. **Multiple subscribers**: Multiple subscriptions to same observable
4. **Broadcasting**: Server broadcasts values to all clients
5. **Rx operators**: Buffer, Where, Select on server streams

## Why This Approach?

Direct IObservable marshaling requires MessagePack formatter with special configuration. The notification-based approach:
- ✅ Works with default JSON formatter
- ✅ More explicit and easier to debug
- ✅ Standard pattern in production RPC systems (gRPC, SignalR)
- ✅ Full Rx operator support on client side
- ✅ No special serialization requirements

## Dependencies

- StreamJsonRpc 2.24.84
- System.Reactive 7.0.0-preview.1
- .NET 10.0