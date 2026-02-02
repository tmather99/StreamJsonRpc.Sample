using System.Threading.Tasks;

namespace StreamJsonRpc.Reactive.Server
{
    // Interface for callbacks that server will invoke on client
    public interface IClientCallbacks
    {
        Task OnNextValue(int value);
        Task OnError(string error);
        Task OnCompleted();
    }
}