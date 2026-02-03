using System.Threading.Tasks;

// Shared code between client and server
public interface INumberStreamListener
{
    Task OnNextValue(int value);
    Task OnError(string error);
    Task OnCompleted();
}