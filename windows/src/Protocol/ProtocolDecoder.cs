using System.Buffers.Binary;
using System.Text.Json;
using NotificationBridge.Windows.Domain;

namespace NotificationBridge.Windows.Protocol;

public enum DecodeStatus { Ok, MalformedJson, ValidationFailed }

public sealed record PairRequestPayload(string DeviceId, string? DeviceName, string Proof);

public sealed record AuthenticatePayload(string DeviceId, string Nonce, string Timestamp, string Proof);

public sealed record DecodedMessage(
    int ProtocolVersion,
    string MessageType,
    string MessageId,
    DateTimeOffset Timestamp,
    NotificationPayload? Notification,
    string? RemovedNotificationId,
    PairRequestPayload? PairRequest,
    AuthenticatePayload? Authenticate
);

public sealed record DecodeResult(DecodeStatus Status, DecodedMessage? Message, IReadOnlyList<string> Errors)
{
    public static DecodeResult Ok(DecodedMessage message) => new(DecodeStatus.Ok, message, Array.Empty<string>());
    public static DecodeResult Fail(DecodeStatus status, params string[] errors) => new(status, null, errors);
}

public static class ProtocolLimits
{
    public const int AppNameMax = 256;
    public const int TitleMax = 1024;
    public const int BodyMax = 8192;
    public const int ExpandedLinesMax = 100;
    public const int ExpandedLineMax = 4096;
    public const int IconBase64Max = 64 * 1024;
    public const int IconMaxDimension = 256;
}

public static class ProtocolDecoder
{
    private static readonly HashSet<string> KnownMessageTypes = new(StringComparer.Ordinal)
    {
        "HELLO", "PAIR_REQUEST", "PAIR_RESPONSE", "AUTHENTICATE", "AUTH_RESULT",
        "HEARTBEAT", "NOTIFICATION", "NOTIFICATION_REMOVED", "ACK", "ERROR"
    };

    public static DecodeResult Decode(string rawJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            return DecodeResult.Fail(DecodeStatus.MalformedJson, $"Invalid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var errors = new List<string>();

            var protocolVersion = 0;
            if (!root.TryGetProperty("protocolVersion", out var versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number || !versionEl.TryGetInt32(out protocolVersion))
                errors.Add("Missing or invalid protocolVersion");
            else if (protocolVersion != 1)
                errors.Add($"Unsupported protocolVersion: {protocolVersion}");

            var messageType = GetString(root, "messageType") ?? "";
            if (string.IsNullOrWhiteSpace(messageType))
                errors.Add("Missing or invalid messageType");
            else if (!KnownMessageTypes.Contains(messageType))
                errors.Add($"Unknown messageType: {messageType}");

            var messageId = GetString(root, "messageId") ?? "";
            if (string.IsNullOrWhiteSpace(messageId))
                errors.Add("Missing or invalid messageId");

            var timestampRaw = GetString(root, "timestamp");
            if (timestampRaw is null || !DateTimeOffset.TryParse(timestampRaw, out var timestamp))
            {
                errors.Add("Missing or invalid timestamp");
                timestamp = default;
            }

            NotificationPayload? notification = null;
            string? removedNotificationId = null;
            PairRequestPayload? pairRequest = null;
            AuthenticatePayload? authenticate = null;

            if (errors.Count == 0 && messageType == "NOTIFICATION")
            {
                if (!root.TryGetProperty("payload", out var payloadEl) || payloadEl.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Missing payload for NOTIFICATION message");
                }
                else
                {
                    var (parsedPayload, payloadErrors) = DecodeNotificationPayload(payloadEl);
                    errors.AddRange(payloadErrors);
                    notification = parsedPayload;
                }
            }
            else if (errors.Count == 0 && messageType == "NOTIFICATION_REMOVED")
            {
                if (!root.TryGetProperty("payload", out var payloadEl) || payloadEl.ValueKind != JsonValueKind.Object ||
                    string.IsNullOrWhiteSpace(removedNotificationId = GetString(payloadEl, "notificationId")))
                {
                    errors.Add("payload.notificationId is required for NOTIFICATION_REMOVED");
                    removedNotificationId = null;
                }
            }
            else if (errors.Count == 0 && messageType == "PAIR_REQUEST")
            {
                if (!root.TryGetProperty("payload", out var payloadEl) || payloadEl.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Missing payload for PAIR_REQUEST message");
                }
                else
                {
                    var deviceId = GetString(payloadEl, "deviceId") ?? "";
                    var proof = GetString(payloadEl, "proof") ?? "";
                    if (string.IsNullOrWhiteSpace(deviceId))
                        errors.Add("payload.deviceId is required for PAIR_REQUEST");
                    if (string.IsNullOrWhiteSpace(proof))
                        errors.Add("payload.proof is required for PAIR_REQUEST");

                    if (errors.Count == 0)
                        pairRequest = new PairRequestPayload(deviceId, GetString(payloadEl, "deviceName"), proof);
                }
            }
            else if (errors.Count == 0 && messageType == "AUTHENTICATE")
            {
                if (!root.TryGetProperty("payload", out var payloadEl) || payloadEl.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Missing payload for AUTHENTICATE message");
                }
                else
                {
                    var deviceId = GetString(payloadEl, "deviceId") ?? "";
                    var nonce = GetString(payloadEl, "nonce") ?? "";
                    var authTimestamp = GetString(payloadEl, "timestamp") ?? "";
                    var proof = GetString(payloadEl, "proof") ?? "";
                    if (string.IsNullOrWhiteSpace(deviceId))
                        errors.Add("payload.deviceId is required for AUTHENTICATE");
                    if (string.IsNullOrWhiteSpace(nonce))
                        errors.Add("payload.nonce is required for AUTHENTICATE");
                    if (string.IsNullOrWhiteSpace(authTimestamp) || !DateTimeOffset.TryParse(authTimestamp, out _))
                        errors.Add("payload.timestamp is missing or invalid for AUTHENTICATE");
                    if (string.IsNullOrWhiteSpace(proof))
                        errors.Add("payload.proof is required for AUTHENTICATE");

                    if (errors.Count == 0)
                        authenticate = new AuthenticatePayload(deviceId, nonce, authTimestamp, proof);
                }
            }

            if (errors.Count > 0)
                return DecodeResult.Fail(DecodeStatus.ValidationFailed, errors.ToArray());

            var message = new DecodedMessage(
                protocolVersion, messageType, messageId, timestamp, notification, removedNotificationId,
                pairRequest, authenticate);
            return DecodeResult.Ok(message);
        }
    }

    private static (NotificationPayload? Payload, List<string> Errors) DecodeNotificationPayload(JsonElement payload)
    {
        var errors = new List<string>();

        var notificationId = GetString(payload, "notificationId") ?? "";
        if (string.IsNullOrWhiteSpace(notificationId))
            errors.Add("payload.notificationId is required");

        var packageName = GetString(payload, "packageName") ?? "";
        if (string.IsNullOrWhiteSpace(packageName))
            errors.Add("payload.packageName is required");

        var appName = GetString(payload, "appName");
        if (appName is { Length: > ProtocolLimits.AppNameMax })
            errors.Add($"payload.appName exceeds {ProtocolLimits.AppNameMax} characters");

        var title = GetString(payload, "title");
        if (title is { Length: > ProtocolLimits.TitleMax })
            errors.Add($"payload.title exceeds {ProtocolLimits.TitleMax} characters");

        var body = GetString(payload, "body");
        if (body is { Length: > ProtocolLimits.BodyMax })
            errors.Add($"payload.body exceeds {ProtocolLimits.BodyMax} characters");

        List<string>? expandedLines = null;
        if (payload.TryGetProperty("expandedLines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
        {
            if (linesEl.GetArrayLength() > ProtocolLimits.ExpandedLinesMax)
            {
                errors.Add($"payload.expandedLines exceeds {ProtocolLimits.ExpandedLinesMax} entries");
            }
            else
            {
                expandedLines = new List<string>();
                foreach (var lineEl in linesEl.EnumerateArray())
                {
                    var line = lineEl.GetString() ?? "";
                    if (line.Length > ProtocolLimits.ExpandedLineMax)
                        errors.Add($"payload.expandedLines entry exceeds {ProtocolLimits.ExpandedLineMax} characters");
                    expandedLines.Add(line);
                }
            }
        }

        if (errors.Count > 0)
            return (null, errors);

        var result = new NotificationPayload(
            notificationId,
            packageName,
            appName,
            title,
            body,
            expandedLines,
            GetString(payload, "summary"),
            GetString(payload, "timestamp"),
            GetString(payload, "category"),
            DecodeIcon(GetString(payload, "iconPng")));

        return (result, errors);
    }

    // Unlike over-limit text, an unusable icon never fails validation: the notification is still
    // shown, just without an icon. Only a small, PNG-signed image with sane dimensions is passed on,
    // so a malformed or decompression-bomb image never reaches the UI's image decoder.
    private static byte[]? DecodeIcon(string? base64)
    {
        if (string.IsNullOrEmpty(base64) || base64.Length > ProtocolLimits.IconBase64Max)
            return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        // 8-byte PNG signature, then the IHDR chunk: 4-byte length, "IHDR", width, height (big-endian).
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(pngSignature))
            return null;

        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width < 1 || height < 1 || width > ProtocolLimits.IconMaxDimension || height > ProtocolLimits.IconMaxDimension)
            return null;

        return bytes;
    }

    private static string? GetString(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
