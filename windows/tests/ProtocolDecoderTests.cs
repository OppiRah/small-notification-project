using NotificationBridge.Windows.Protocol;
using Xunit;

namespace NotificationBridge.Windows.Tests;

public class ProtocolDecoderTests
{
    private static string ValidNotificationJson(string overridesJson = "") => $$"""
    {
      "protocolVersion": 1,
      "messageType": "NOTIFICATION",
      "messageId": "test-message-1",
      "timestamp": "2026-09-23T10:00:00Z",
      "payload": {
        "notificationId": "n1",
        "packageName": "com.test.app",
        "appName": "Test",
        "title": "Hello",
        "body": "World",
        "expandedLines": [],
        "summary": null,
        "timestamp": "2026-09-23T10:00:00Z",
        "category": "message"
      }
    }
    """;

    [Fact]
    public void ValidNotification_DecodesSuccessfully()
    {
        var result = ProtocolDecoder.Decode(ValidNotificationJson());

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.NotNull(result.Message);
        Assert.Equal("NOTIFICATION", result.Message!.MessageType);
        Assert.NotNull(result.Message.Notification);
        Assert.Equal("com.test.app", result.Message.Notification!.PackageName);
    }

    [Fact]
    public void NotificationRemoved_DecodesSuccessfully()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION_REMOVED",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "notificationId": "n1"
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal("n1", result.Message!.RemovedNotificationId);
    }

    [Fact]
    public void NotificationRemoved_MissingNotificationId_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION_REMOVED",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {}
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("notificationId"));
    }

    [Fact]
    public void InvalidJson_IsRejectedAsMalformed()
    {
        var result = ProtocolDecoder.Decode("{ not valid json ");

        Assert.Equal(DecodeStatus.MalformedJson, result.Status);
        Assert.Null(result.Message);
    }

    [Fact]
    public void UnknownMessageType_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "NOT_A_REAL_TYPE",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {}
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("Unknown messageType"));
    }

    [Fact]
    public void UnsupportedProtocolVersion_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 2,
          "messageType": "HEARTBEAT",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {}
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("Unsupported protocolVersion"));
    }

    [Fact]
    public void MissingRequiredField_IsRejected()
    {
        var json = """
        {
          "messageType": "HEARTBEAT",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {}
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("protocolVersion"));
    }

    [Fact]
    public void OversizedTitle_IsRejected()
    {
        var oversizedTitle = new string('a', ProtocolLimits.TitleMax + 1);
        var json = $$"""
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "notificationId": "n1",
            "packageName": "com.test.app",
            "title": "{{oversizedTitle}}"
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("title exceeds"));
    }

    [Fact]
    public void InvalidTimestamp_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "HEARTBEAT",
          "messageId": "test-message-1",
          "timestamp": "not-a-timestamp",
          "payload": {}
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("timestamp"));
    }

    [Fact]
    public void MalformedNotificationPayload_MissingRequiredPayloadFields_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "NOTIFICATION",
          "messageId": "",
          "payload": {
            "packageName": "com.example.test"
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        // Envelope-level validation fails first (empty messageId, missing timestamp), so the
        // decoder short-circuits before it ever validates payload.notificationId.
        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("messageId"));
        Assert.Contains(result.Errors, e => e.Contains("timestamp"));
    }
}
