public class RegistryChangeEvent
{
    public DateTime Time { get; set; }
    public string KeyPath { get; set; } = "";
    public string? ValueName { get; set; }
    public string? ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public string? AccessTypeRaw { get; set; }
    public RegistryAuditEventId AuditEventId { get; set; }
    public RegistryOperationType OperationType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public enum RegistryOperationType
{
    Unknown,
    KeyCreated,
    KeyDeleted,
    ValueUpdated,
    PermissionChanged
}

public enum RegistryAuditEventId
{
    RegistryValueModified = 4657,
    RegistryObjectDeleted = 4660,
    RegistryPermissionsChanged = 4670,
    RegistryKeyAccessed = 4663
}
