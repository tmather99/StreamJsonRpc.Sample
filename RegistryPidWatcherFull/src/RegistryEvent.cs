namespace RegistryPidWatcher;

public sealed class RegistryEvent
{
    public int Pid { get; init; }
    public string Process { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string ValueName { get; init; } = string.Empty;
    public string AccessMaskRaw { get; init; } = string.Empty;
    public string AccessMaskText { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
    public string InferredAction { get; init; } = string.Empty;
}
