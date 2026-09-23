using System.Text.Json;

namespace NotificationBridge.Windows.Protocol;

// Builds wire messages the PC sends back to the phone -- pairing/auth responses only. Outgoing
// NOTIFICATION traffic doesn't exist on this side (Android is always the notification source).
public static class ProtocolMessages
{
    public static string PairResponseSuccess(string deviceId, byte[] sharedSecret, string pcName)
    {
        var payload = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["deviceId"] = deviceId,
            ["sharedSecret"] = Convert.ToBase64String(sharedSecret),
            ["pcName"] = pcName,
        };
        return Envelope("PAIR_RESPONSE", payload);
    }

    public static string PairResponseFailure(string error)
    {
        var payload = new Dictionary<string, object?> { ["success"] = false, ["error"] = error };
        return Envelope("PAIR_RESPONSE", payload);
    }

    public static string AuthResult(bool success, string? error = null)
    {
        var payload = new Dictionary<string, object?> { ["success"] = success, ["error"] = error };
        return Envelope("AUTH_RESULT", payload);
    }

    private static string Envelope(string messageType, Dictionary<string, object?> payload)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["protocolVersion"] = 1,
            ["messageType"] = messageType,
            ["messageId"] = Guid.NewGuid().ToString(),
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["payload"] = payload,
        };
        return JsonSerializer.Serialize(envelope);
    }
}
