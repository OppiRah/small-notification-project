using System.Net.WebSockets;
using System.Text;

namespace NotificationBridge.Windows.Transport;

public static class SyntheticTestClient
{
    public static async Task SendAsync(int port, string json)
    {
        using var client = new ClientWebSocket();
        var uri = new Uri($"ws://127.0.0.1:{port}/ws/");
        await client.ConnectAsync(uri, CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }
}
