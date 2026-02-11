using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;

namespace RegistryMonitor;

public class EventLogParser
{
    private readonly string _registryPath;

    public EventLogParser(string registryPath)
    {
        _registryPath = registryPath;
    }

    private static void PrettyPrintXml(EventRecord e)
    {
        var xml = e.ToXml();
        var doc = XDocument.Parse(xml);
        Console.WriteLine("\n" + doc.ToString(SaveOptions.None) + "\n"); // default formatting with indentation
    }

    public IEnumerable<(int pid, string process, string key, string valueName)> ReadEvents()
    {
        int delta = 5;
        int ms = delta * 1000;

        // 4657 = value change,
        // 4659/4660/4663 commonly show deletes / handle operations
        string timeFilter =
            $"""
            *[System[
                (EventID=4657 or EventID=4659 or EventID=4660 or EventID=4663)
                and TimeCreated[timediff(@SystemTime) <= {ms}]
            ]]
            """;

        EventLogQuery query = new("Security", PathType.LogName, timeFilter) {
            ReverseDirection = true,
            TolerateQueryErrors = true
        };

        using EventLogReader reader = new(query);
        for (EventRecord evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
        {
            if (Program.DumpXml)
            {
                PrettyPrintXml(evt);
            }

            if (evt.TimeCreated == null) continue;

            var xml = evt.ToXml();
            string rawPid = Extract(xml, "ProcessId");
            string proc = Extract(xml, "ProcessName");
            string key = Extract(xml, "ObjectName");
            string valueName = Extract(xml, "ObjectValueName");

            if (string.IsNullOrEmpty(rawPid))
                continue; // skip events that don't have process info

            int pid = rawPid.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(rawPid[2..], 16)
                : int.Parse(rawPid);

            yield return (pid, proc, key, valueName);
        }
    }

    private static string Extract(string xml, string name)
    {
        var tag = $"Name='{name}'";
        var i = xml.IndexOf(tag);
        if (i < 0) return "";
        var start = xml.IndexOf(">", i) + 1;
        var end = xml.IndexOf("<", start);
        return xml[start..end];
    }
}