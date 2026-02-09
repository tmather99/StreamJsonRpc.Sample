namespace RegistryListener;

public record RegistryChangeEvent(
    DateTime? Time,
    string KeyPath,
    string ValueName,
    int ProcessId,
    string ProcessName);