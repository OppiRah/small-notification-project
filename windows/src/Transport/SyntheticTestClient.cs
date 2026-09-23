using System.Net.WebSockets;
using System.Text;

namespace NotificationBridge.Windows.Transport;

// Dev-only test client: connects over wss:// like a real device would, but skips certificate
// validation since it's talking to our own freshly-generated self-signed cert on localhost. A
// real device instead pins the certificate's fingerprint during pairing (ADR-009).
public static class SyntheticTestClient
{
    public static async Task SendAsync(int port, string json)
    {
        using var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        var uri = new Uri($"wss://127.0.0.1:{port}/ws/");
        await client.ConnectAsync(uri, CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }
}
