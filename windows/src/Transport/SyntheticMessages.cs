namespace NotificationBridge.Windows.Transport;

public static class SyntheticMessages
{
    public static string ValidNotification()
    {
        var id = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        return $$"""
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION",
          "messageId": "{{id}}",
          "timestamp": "{{timestamp}}",
          "payload": {
            "notificationId": "synthetic-1",
            "packageName": "com.example.test",
            "appName": "Synthetic Test",
            "title": "Test notification",
            "body": "This is a synthetic test message sent from the Windows dev panel.",
            "expandedLines": [],
            "summary": null,
            "timestamp": "{{timestamp}}",
            "category": "message"
          }
        }
        """;
    }

    public static string MalformedNotification()
    {
        return """
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION",
          "messageId": "",
          "payload": {
            "packageName": "com.example.test"
          }
        }
        """;
    }
}
