namespace StreamJsonRpc.Aot.Common;

// Shared interface between client and server
public partial interface IStreamListener<T>
{
    Task OnNextValue(T value);
    Task OnError(string error);
    Task OnCompleted();
}