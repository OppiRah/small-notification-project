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
    public void PairRequest_DecodesSuccessfully()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "PAIR_REQUEST",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "deviceId": "device-1",
            "deviceName": "My Phone",
            "proof": "c29tZS1wcm9vZg=="
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal("device-1", result.Message!.PairRequest!.DeviceId);
        Assert.Equal("My Phone", result.Message.PairRequest.DeviceName);
    }

    [Fact]
    public void PairRequest_MissingProof_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "PAIR_REQUEST",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "deviceId": "device-1"
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("proof"));
    }

    [Fact]
    public void Authenticate_DecodesSuccessfully()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "AUTHENTICATE",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "deviceId": "device-1",
            "nonce": "nonce-1",
            "timestamp": "2026-09-23T10:00:00Z",
            "proof": "c29tZS1wcm9vZg=="
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal("device-1", result.Message!.Authenticate!.DeviceId);
        Assert.Equal("nonce-1", result.Message.Authenticate.Nonce);
    }

    [Fact]
    public void Authenticate_InvalidTimestamp_IsRejected()
    {
        var json = """
        {
          "protocolVersion": 1,
          "messageType": "AUTHENTICATE",
          "messageId": "test-message-1",
          "timestamp": "2026-09-23T10:00:00Z",
          "payload": {
            "deviceId": "device-1",
            "nonce": "nonce-1",
            "timestamp": "not-a-timestamp",
            "proof": "c29tZS1wcm9vZg=="
          }
        }
        """;

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("timestamp"));
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
    public void BodyExactlyAtLimit_IsAccepted()
    {
        var json = ValidNotificationJson().Replace("\"World\"", $"\"{new string('a', ProtocolLimits.BodyMax)}\"");

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal(ProtocolLimits.BodyMax, result.Message!.Notification!.Body!.Length);
    }

    [Fact]
    public void BodyOverLimit_IsRejected()
    {
        var json = ValidNotificationJson().Replace("\"World\"", $"\"{new string('a', ProtocolLimits.BodyMax + 1)}\"");

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("body exceeds"));
    }

    [Fact]
    public void TooManyExpandedLines_IsRejected()
    {
        var lines = string.Join(",", Enumerable.Repeat("\"x\"", ProtocolLimits.ExpandedLinesMax + 1));
        var json = ValidNotificationJson().Replace("\"expandedLines\": []", $"\"expandedLines\": [{lines}]");

        var result = ProtocolDecoder.Decode(json);

        Assert.Equal(DecodeStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, e => e.Contains("expandedLines exceeds"));
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
