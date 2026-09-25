using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NotificationBridge.Windows.Security;
using NotificationBridge.Windows.Transport;
using Xunit;

namespace NotificationBridge.Windows.Tests;

public class ReceiverResilienceTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly int _port;
    private readonly LocalWebSocketReceiver _receiver;
    private readonly List<bool> _connectionEvents = new();

    public ReceiverResilienceTests()
    {
        _port = GetFreePort();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfx = cert.Export(X509ContentType.Pfx);

        _receiver = new LocalWebSocketReceiver(
            _port,
            X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.PersistKeySet),
            new PairingSession(),
            new TrustedDeviceStore());
        _receiver.ClientConnectionChanged += connected =>
        {
            lock (_connectionEvents) _connectionEvents.Add(connected);
        };
        _receiver.Start();
    }

    public void Dispose() => _receiver.Stop();

    [Fact]
    public async Task OversizedMessageBeforeAuthentication_IsRejectedAndConnectionClosed()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        using var client = await ConnectAsync(cts.Token);

        var oversized = Encoding.UTF8.GetBytes(new string('x', 256 * 1024));
        try
        {
            await client.SendAsync(oversized, WebSocketMessageType.Text, true, cts.Token);
        }
        catch (WebSocketException)
        {
            // The server may abort mid-send once the limit is exceeded; that still counts as rejected.
            return;
        }

        var closeFrame = await ReceiveUntilCloseAsync(client, cts.Token);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeFrame);
    }

    [Fact]
    public async Task ClientConnectionChanged_StaysTrueUntilLastConnectionCloses()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        using var first = await ConnectAsync(cts.Token);
        using var second = await ConnectAsync(cts.Token);
        await WaitForEventCountAsync(2, cts.Token);

        await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
        await WaitForEventCountAsync(3, cts.Token);
        Assert.True(LastEvent(), "a second client is still connected");

        await second.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
        await WaitForEventCountAsync(4, cts.Token);
        Assert.False(LastEvent());
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken token)
    {
        var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{_port}/ws/"), token);
        return client;
    }

    private static async Task<WebSocketCloseStatus?> ReceiveUntilCloseAsync(ClientWebSocket client, CancellationToken token)
    {
        var buffer = new byte[1024];
        try
        {
            while (true)
            {
                var result = await client.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return result.CloseStatus;
            }
        }
        catch (WebSocketException)
        {
            return WebSocketCloseStatus.MessageTooBig;
        }
    }

    private async Task WaitForEventCountAsync(int count, CancellationToken token)
    {
        while (true)
        {
            lock (_connectionEvents)
            {
                if (_connectionEvents.Count >= count) return;
            }
            await Task.Delay(20, token);
        }
    }

    private bool LastEvent()
    {
        lock (_connectionEvents) return _connectionEvents[^1];
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
