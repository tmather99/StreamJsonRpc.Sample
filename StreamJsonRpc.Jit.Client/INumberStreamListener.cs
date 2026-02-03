using System.Threading.Tasks;

// Shared interface between client and server
public interface IListener
{
    Task OnNextValue(int value);
    Task OnError(string error);
    Task OnCompleted();
}

// Concreate interface
public interface INumberStreamListener : IListener
{
}