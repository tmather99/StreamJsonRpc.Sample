using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace StreamJsonRpc.Jit.Client
{
    // Client-side implementation that receives callbacks from server
    public class NumberStreamStreamListener : INumberStreamStreamListener
    {
        private readonly Subject<int> _subject = new Subject<int>();

        public IObservable<int> Values => _subject;

        public Task OnNextValue(int value)
        {
            Console.WriteLine($"        OnNextValue: {value}");
            _subject.OnNext(value);
            return Task.CompletedTask;
        }

        public Task OnError(string error)
        {
            Console.WriteLine($"        Stream error: {error}");
            _subject.OnError(new Exception(error));
            return Task.CompletedTask;
        }

        public Task OnCompleted()
        {
            Console.WriteLine("        Stream completed");
            _subject.OnCompleted();
            return Task.CompletedTask;
        }
    }
}