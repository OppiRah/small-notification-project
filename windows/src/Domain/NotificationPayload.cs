namespace NotificationBridge.Windows.Domain;

public sealed record NotificationPayload(
    string NotificationId,
    string PackageName,
    string? AppName,
    string? Title,
    string? Body,
    List<string>? ExpandedLines,
    string? Summary,
    string? Timestamp,
    string? Category
);
