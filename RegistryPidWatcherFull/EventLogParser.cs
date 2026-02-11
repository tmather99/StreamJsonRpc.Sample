using System.Diagnostics.Eventing.Reader;
using System.Xml;
using System.Xml.Linq;

namespace RegistryMonitor;

public class EventLogParser
{
    private readonly string _registryPath;

    public EventLogParser(string registryPath)
    {
        _registryPath = registryPath;
    }

    private static void PrettyPrintXml(string xml)
    {
        XDocument doc = XDocument.Parse(xml);
        Console.WriteLine("\n" + doc.ToString(SaveOptions.None) + "\n");
    }

    public IEnumerable<(int pid, string process, string key, string valueName,
        string accessMaskRaw, string accessMaskText, string operationType, string newValue, string inferredAction)> ReadEvents()
    {
        int delta = 5;
        int ms = delta * 1000;

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
            if (evt.TimeCreated == null) continue;

            string xml = evt.ToXml();
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (Program.DumpXml)
            {
                PrettyPrintXml(xml);
            }

            string Get(string name)
            {
                var node = doc.SelectSingleNode(
                    "/*[local-name()='Event']/*[local-name()='EventData']/*[local-name()='Data' and @Name='" + name + "']");
                return node?.InnerText?.Trim() ?? string.Empty;
            }

            static string DecodeRegistryAccessMask(string hex)
            {
                if (string.IsNullOrWhiteSpace(hex))
                    return string.Empty;

                int mask = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(hex[2..], 16)
                    : Convert.ToInt32(hex, 16);

                var parts = new List<string>();

                if ((mask & 0x1) != 0) parts.Add("KEY_QUERY_VALUE");
                if ((mask & 0x2) != 0) parts.Add("KEY_SET_VALUE");
                if ((mask & 0x4) != 0) parts.Add("KEY_CREATE_SUB_KEY");
                if ((mask & 0x8) != 0) parts.Add("KEY_ENUMERATE_SUB_KEYS");
                if ((mask & 0x10) != 0) parts.Add("KEY_NOTIFY");
                if ((mask & 0x20) != 0) parts.Add("KEY_CREATE_LINK");

                if ((mask & 0x00010000) != 0) parts.Add("DELETE");
                if ((mask & 0x00020000) != 0) parts.Add("READ_CONTROL");
                if ((mask & 0x00040000) != 0) parts.Add("WRITE_DAC");
                if ((mask & 0x00080000) != 0) parts.Add("WRITE_OWNER");

                if ((mask & unchecked((int)0x80000000)) != 0) parts.Add("GENERIC_READ");
                if ((mask & 0x40000000) != 0) parts.Add("GENERIC_WRITE");
                if ((mask & 0x20000000) != 0) parts.Add("GENERIC_EXECUTE");
                if ((mask & 0x10000000) != 0) parts.Add("GENERIC_ALL");

                return parts.Count == 0 ? mask.ToString("X") : string.Join("|", parts);
            }

            static string DecodeOperationType(
                string op,
                string oldValueType, string oldValue,
                string newValueType, string newValue)
            {
                if (op == "%%1906" &&
                    !string.IsNullOrEmpty(oldValueType) &&
                    (string.Equals(newValueType, "-", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(newValue, "-", StringComparison.OrdinalIgnoreCase)))
                {
                    return "ValueDeleted";
                }

                if (op == "%%1905" &&
                    (string.IsNullOrEmpty(oldValueType) ||
                     string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase)) &&
                    string.IsNullOrEmpty(oldValue) &&
                    !string.IsNullOrEmpty(newValue) &&
                    !string.Equals(newValue, "-", StringComparison.OrdinalIgnoreCase))
                {
                    return "ValueCreated";
                }

                if (op == "%%1905" &&
                    (!string.IsNullOrEmpty(oldValue) ||
                     (!string.IsNullOrEmpty(oldValueType) &&
                      !string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase))))
                {
                    return "ValueModified";
                }

                if (op == "%%1904" &&
                    (string.IsNullOrEmpty(oldValueType) ||
                     string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(oldValue) ||
                     string.Equals(oldValue, "-", StringComparison.OrdinalIgnoreCase)))
                {
                    return "ValueCreated";
                }

                return op switch {
                    "%%1904" => "ValueDeleted",
                    "%%1905" => "ValueCreated",
                    "%%1906" => "ValueModified",
                    _ => op
                };
            }

            string rawPid = Get("ProcessId");
            string proc = Get("ProcessName");
            string key = Get("ObjectName");

            if (!string.IsNullOrEmpty(_registryPath))
            {
                string expectedPrefix = "\\REGISTRY\\" + _registryPath;
                if (key == null || !key.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            string valueName = Get("ObjectValueName");
            string oldValueType = Get("OldValueType");
            string oldValue = Get("OldValue");
            string newValueType = Get("NewValueType"); if (!string.IsNullOrEmpty(_registryPath))
            {
                string newValue = Get("NewValue");
                string accessMaskRaw = Get("AccessMask");
                string accessMaskText = DecodeRegistryAccessMask(accessMaskRaw);
                string operationTypeRaw = Get("OperationType");
                string operationType = DecodeOperationType(operationTypeRaw, oldValueType, oldValue, newValueType, newValue);

                string inferredAction;
                if (!string.IsNullOrEmpty(valueName))
                {
                    inferredAction = operationType;
                }
                else if (!string.IsNullOrEmpty(accessMaskRaw))
                {
                    inferredAction = accessMaskText.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                        ? "KeyDeleteOrHandleClose"
                        : "KeyAccess";
                }
                else
                {
                    inferredAction = string.Empty;
                }

                if (string.IsNullOrEmpty(rawPid))
                    continue;

                int pid = rawPid.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(rawPid[2..], 16)
                    : int.Parse(rawPid);

                yield return (pid, proc, key, valueName, accessMaskRaw, accessMaskText, operationType, newValue, inferredAction);
            }
        }
    }
}