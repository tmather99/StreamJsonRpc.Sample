using System.Diagnostics.Eventing.Reader;

namespace RegistryListener;

public class RegistryEventSource : IObservable<RegistryChangeEvent>
{
    private readonly List<IObserver<RegistryChangeEvent>> _observers = new();
    private readonly string _filterKey;
    private EventLogWatcher? _watcher;

    public RegistryEventSource(string filterKey) => _filterKey = filterKey;

    public IDisposable Subscribe(IObserver<RegistryChangeEvent> observer)
    {
        _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }

    public void Start()
    {
        string query = @"
        <QueryList>
          <Query Id='0' Path='Security'>
            <Select Path='Security'>*[System[(EventID=4657)]]</Select>
          </Query>
        </QueryList>";

        _watcher = new EventLogWatcher(new EventLogQuery("Security", PathType.LogName, query));
        _watcher.EventRecordWritten += OnEvent;
        _watcher.Enabled = true;
    }

    public void Stop() => _watcher?.Dispose();

    private void OnEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null) return;

        using var r = e.EventRecord;
        var p = r.Properties;

        for (int i = 0; i < r.Properties.Count; i++)
        {
            Console.WriteLine($"{i}: {r.Properties[i].Value}");
        }


        string keyPath = p[5].Value?.ToString() ?? "";
        if (!keyPath.Contains(_filterKey, StringComparison.OrdinalIgnoreCase))
            return;

        var evt = new RegistryChangeEvent(
            r.TimeCreated,
            keyPath,
            p[6].Value?.ToString() ?? "",
            int.TryParse(p[9].Value?.ToString(), out var pid) ? pid : 0,
            p[10].Value?.ToString() ?? "");

        foreach (var o in _observers)
            o.OnNext(evt);
    }

    private class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<RegistryChangeEvent>> _obs;
        private readonly IObserver<RegistryChangeEvent> _observer;

        public Unsubscriber(List<IObserver<RegistryChangeEvent>> obs, IObserver<RegistryChangeEvent> observer)
        {
            _obs = obs;
            _observer = observer;
        }

        public void Dispose() => _obs.Remove(_observer);
    }
}
