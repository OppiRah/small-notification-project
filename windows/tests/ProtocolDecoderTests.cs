using System.Buffers.Binary;
using System.Text;
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

    // Minimal PNG prefix: signature + IHDR length/type + width/height. The decoder only inspects
    // these bytes; pixel decoding is left to the UI layer.
    private static string PngHeaderBase64(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return Convert.ToBase64String(bytes);
    }

    private static string WithIcon(string iconValue) =>
        ValidNotificationJson().Replace("\"category\": \"message\"", $"\"category\": \"message\", \"iconPng\": \"{iconValue}\"");

    [Fact]
    public void NotificationWithoutIcon_HasNoIcon()
    {
        var result = ProtocolDecoder.Decode(ValidNotificationJson());

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Null(result.Message!.Notification!.IconPng);
    }

    [Fact]
    public void ValidIcon_IsDecoded()
    {
        var result = ProtocolDecoder.Decode(WithIcon(PngHeaderBase64(96, 96)));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.NotNull(result.Message!.Notification!.IconPng);
    }

    // An icon must never cost the user the notification itself, so an unusable icon is dropped
    // rather than failing validation (unlike over-limit text).
    [Theory]
    [InlineData("not base64 at all!!!")]
    [InlineData("aGVsbG8gd29ybGQsIHRoaXMgaXMgbm90IGEgcG5nIGZpbGUgYXQgYWxs")]
    public void UnusableIcon_IsDroppedButNotificationAccepted(string iconValue)
    {
        var result = ProtocolDecoder.Decode(WithIcon(iconValue));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Null(result.Message!.Notification!.IconPng);
        Assert.Equal("Hello", result.Message.Notification.Title);
    }

    [Fact]
    public void IconWithOversizedDimensions_IsDroppedButNotificationAccepted()
    {
        var result = ProtocolDecoder.Decode(WithIcon(PngHeaderBase64(5000, 5000)));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Null(result.Message!.Notification!.IconPng);
    }

    [Fact]
    public void IconLongerThanLimit_IsDroppedButNotificationAccepted()
    {
        var result = ProtocolDecoder.Decode(WithIcon(new string('A', ProtocolLimits.IconBase64Max + 4)));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Null(result.Message!.Notification!.IconPng);
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
