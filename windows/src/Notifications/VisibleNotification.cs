namespace NotificationBridge.Windows.Notifications;

public sealed class VisibleNotification
{
    public required string Id { get; init; }
    public required string PackageName { get; init; }
    public string? AppName { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public byte[]? IconPng { get; init; }
}
