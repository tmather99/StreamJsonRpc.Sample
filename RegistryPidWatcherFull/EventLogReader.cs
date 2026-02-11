using System.Diagnostics.Eventing.Reader;
using System.Xml;
using System.Xml.Linq;

namespace RegistryMonitor;

// This class is responsible for reading and parsing registry-related events from the Windows Security event log.
public class EventLogReader
{
    private readonly string _registryPath;

    public EventLogReader(string registryPath)
    {
        _registryPath = registryPath;
    }

    private static void PrettyPrintXml(string xml)
    {
        XDocument doc = XDocument.Parse(xml);
        Console.WriteLine("\n" + doc.ToString(SaveOptions.None) + "\n");
    }

    // Reads recent registry-related events from the Security event log, filtering by the specified registry path.
    public IEnumerable<(int pid,
                        string process,
                        string key,
                        string valueName,
                        string accessMaskRaw,
                        string accessMaskText,
                        string operationType,
                        string newValue,
                        string inferredAction)> ReadEvents()
    {
        //
        // Capture events from the last few seconds.
        //
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

        // Note: If the Security log is very busy, this query might still miss some events due to the time filter. 
        // For a more robust solution, consider implementing a checkpointing mechanism to track the last read event
        // and query for events after that point in subsequent runs.
        using System.Diagnostics.Eventing.Reader.EventLogReader reader = new(query);

        // Iterate through the events returned by the query, filtering and parsing them as needed.
        for (EventRecord evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
        {
            if (string.IsNullOrEmpty(_registryPath))
            {
                continue;
            }

            if (evt.TimeCreated == null) continue;

            string xml = evt.ToXml();
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (Program.DumpXml)
            {
                PrettyPrintXml(xml);
            }

            string key = Get("ObjectName");

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // Normalize registry path from Security log into the same format as _registryPath
            string normalizedKey = NormalizeRegistryPath(key);

            // skip events that don't match the prefix
            if (string.IsNullOrEmpty(normalizedKey) ||
                !normalizedKey.StartsWith(_registryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string rawPid = Get("ProcessId");
            if (string.IsNullOrEmpty(rawPid)) continue;
            int pid = ParsePid(rawPid);

            string proc = Get("ProcessName");
            string valueName = Get("ObjectValueName");
            string oldValueType = Get("OldValueType");
            string oldValue = Get("OldValue");
            string newValueType = Get("NewValueType");

            string newValue = Get("NewValue");
            string accessMaskRaw = Get("AccessMask");
            string accessMaskText = DecodeRegistryAccessMask(accessMaskRaw);
            string operationTypeRaw = Get("OperationType");
            string operationType = DecodeOperationType(operationTypeRaw, oldValueType, oldValue, newValueType, newValue);

            string inferredAction = InferredAction();

            yield return (pid,
                          proc,
                          key,
                          valueName,
                          accessMaskRaw,
                          accessMaskText,
                          operationType,
                          newValue,
                          inferredAction);

            // Helper method to normalize registry paths from the Security log format to a more standard format for comparison.
            //   expectedPrefix = "\\REGISTRY\\USER\\Software\\MyApp1"
            //   Actual event: \REGISTRY\USER\S-1-5-...-1000\Software\MyApp1
            string NormalizeRegistryPath(string raw)
            {
                // HKLM: \REGISTRY\MACHINE\SOFTWARE\WorkspaceONE\Satori
                if (raw.StartsWith(@"\REGISTRY\MACHINE\", StringComparison.OrdinalIgnoreCase))
                {
                    return "MACHINE\\" + raw.Substring(@"\REGISTRY\MACHINE\".Length);
                }

                // HKCU: \REGISTRY\USER\<sid>\Software\MyApp1
                if (raw.StartsWith(@"\REGISTRY\USER\", StringComparison.OrdinalIgnoreCase))
                {
                    var afterUser = raw.Substring(@"\REGISTRY\USER\".Length);
                    var firstBackslash = afterUser.IndexOf('\\');
                    if (firstBackslash > 0)
                    {
                        // Strip SID, keep path after it: Software\MyApp1\...
                        var withoutSid = afterUser.Substring(firstBackslash + 1);
                        return "USER\\" + withoutSid;
                    }
                }

                return raw;
            }

            // Helper method to extract the value of a specific data field from the event XML.
            string Get(string name)
            {
                var node = doc.SelectSingleNode(
                    "/*[local-name()='Event']/*[local-name()='EventData']/*[local-name()='Data' and @Name='" + name + "']");
                return node?.InnerText?.Trim() ?? string.Empty;
            }

            // Helper method to parse the ProcessId, handling both decimal and hexadecimal formats
            int ParsePid(string rawPid1)
            {
                int i = rawPid1.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(rawPid1[2..], 16)
                    : int.Parse(rawPid1);
                return i;
            }

            // Helper method to infer the action type based on available data
            string InferredAction()
            {
                string s;
                if (!string.IsNullOrEmpty(valueName))
                {
                    s = operationType;
                }
                else if (!string.IsNullOrEmpty(accessMaskRaw))
                {
                    s = accessMaskText.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                        ? "KeyDeleteOrHandleClose"
                        : "KeyAccess";
                }
                else
                {
                    s = string.Empty;
                }

                return s;
            }

            // Helper method to decode the AccessMask into human-readable permissions
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

            // Helper method to decode the OperationType based on the event data and heuristics
            static string DecodeOperationType(string op,
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
        }
    }
}