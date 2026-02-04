using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client;

// Shared interface between client and server
public interface IStreamListener<T>
{
    Task OnNextValue(T value);
    Task OnError(string error);
    Task OnCompleted();
}