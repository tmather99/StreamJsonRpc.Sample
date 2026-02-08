namespace StreamJsonRpc.Aot.Common;

// Shared interface between client and server
public interface IDataStream
{
    Task Subscribe(Guid clientGuid);
    Task Unsubscribe(Guid clientGuid);
}