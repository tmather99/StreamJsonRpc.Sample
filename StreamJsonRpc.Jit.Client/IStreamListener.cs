using System.Threading.Tasks;

// Shared interface between client and server
public interface IStreamListener<T>
{
    Task OnNextValue(T value);
    Task OnError(string error);
    Task OnCompleted();
}