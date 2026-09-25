using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NotificationBridge.Windows.Protocol;
using NotificationBridge.Windows.Security;

namespace NotificationBridge.Windows.Transport;

// TLS-secured local receiver (wss://). Built on TcpListener + SslStream + a hand-rolled RFC 6455
// upgrade handshake rather than HttpListener, because HttpListener's HTTPS support requires an
// admin-elevated `netsh http add sslcert` binding, which is unreasonable friction for a personal
// desktop app. The handshake itself (Sec-WebSocket-Accept via SHA-1 + a fixed magic GUID) is the
// exact RFC-mandated computation, not invented crypto; once upgraded, .NET's own
// WebSocket.CreateFromStream takes over framing, same as HttpListener would have provided.
public sealed class LocalWebSocketReceiver
{
    private const string WebSocketMagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private static readonly TimeSpan AuthTimestampWindow = TimeSpan.FromMinutes(5);

    // Pairing/auth messages are tiny, so unauthenticated peers get a small cap. Authenticated
    // messages are bounded by ProtocolLimits (worst case ~100 expanded lines x 4096 chars).
    private const int MaxPreAuthMessageBytes = 64 * 1024;
    private const int MaxAuthenticatedMessageBytes = 2 * 1024 * 1024;

    // Without a ping/pong timeout a phone that silently drops off Wi-Fi (or a PC that sleeps)
    // leaves a half-open socket that is never detected as dead.
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(15);

    private readonly int _port;
    private readonly X509Certificate2 _certificate;
    private readonly string _certificateFingerprint;
    private readonly PairingSession _pairingSession;
    private readonly TrustedDeviceStore _trustedDevices;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _activeConnections;

    public event Action<string>? MessageReceived;
    public event Action<bool>? ClientConnectionChanged;
    public event Action<string>? AuthorizedNotification;

    public LocalWebSocketReceiver(int port, X509Certificate2 certificate, PairingSession pairingSession, TrustedDeviceStore trustedDevices)
    {
        _port = port;
        _certificate = certificate;
        _certificateFingerprint = CertificateStore.Sha256Fingerprint(certificate);
        _pairingSession = pairingSession;
        _trustedDevices = trustedDevices;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(token);
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested) return;
                continue;
            }

            _ = HandleClientAsync(client, token);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using var _ = client;
        var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);

        try
        {
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, token);
        }
        catch (Exception)
        {
            return;
        }

        var secWebSocketKey = await ReadWebSocketKeyAsync(sslStream, token);
        if (secWebSocketKey is null)
            return;

        await WriteUpgradeResponseAsync(sslStream, secWebSocketKey, token);

        var socket = WebSocket.CreateFromStream(sslStream, new WebSocketCreationOptions
        {
            IsServer = true,
            KeepAliveInterval = KeepAliveInterval,
            KeepAliveTimeout = KeepAliveTimeout,
        });
        ClientConnectionChanged?.Invoke(Interlocked.Increment(ref _activeConnections) > 0);

        var isAuthenticated = false;

        var buffer = new byte[16 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var messageStream = new MemoryStream();
                var maxMessageBytes = isAuthenticated ? MaxAuthenticatedMessageBytes : MaxPreAuthMessageBytes;
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", token);
                        break;
                    }
                    if (messageStream.Length + result.Count > maxMessageBytes)
                    {
                        // CloseOutputAsync (not CloseAsync): don't wait on a peer that is mid-flood.
                        await socket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "message too large", token);
                        return;
                    }
                    messageStream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var text = Encoding.UTF8.GetString(messageStream.ToArray());
                MessageReceived?.Invoke(text);

                isAuthenticated = await RouteMessageAsync(socket, text, isAuthenticated, token);
            }
        }
        catch (Exception)
        {
            // Client dropped the connection; fall through to cleanup below.
        }
        finally
        {
            ClientConnectionChanged?.Invoke(Interlocked.Decrement(ref _activeConnections) > 0);
            socket.Dispose();
        }
    }

    // Returns the connection's authenticated state after handling this message. Pairing and
    // authentication are handled entirely here; NOTIFICATION/NOTIFICATION_REMOVED only reach
    // AuthorizedNotification once this connection has a successful AUTHENTICATE on record, per
    // SECURITY.md #2/#6 ("the PC must reject notification data from untrusted peers").
    private async Task<bool> RouteMessageAsync(WebSocket socket, string rawJson, bool isAuthenticated, CancellationToken token)
    {
        var result = ProtocolDecoder.Decode(rawJson);
        if (result.Status != DecodeStatus.Ok || result.Message is not { } message)
            return isAuthenticated;

        switch (message.MessageType)
        {
            case "PAIR_REQUEST" when message.PairRequest is { } pairRequest:
            {
                if (_pairingSession.VerifyProof(_certificateFingerprint, pairRequest.Proof))
                {
                    var secret = RandomNumberGenerator.GetBytes(32);
                    _trustedDevices.Add(new TrustedDevice(
                        pairRequest.DeviceId,
                        pairRequest.DeviceName ?? pairRequest.DeviceId,
                        Convert.ToBase64String(secret),
                        DateTimeOffset.UtcNow));
                    await SendJsonAsync(socket, ProtocolMessages.PairResponseSuccess(pairRequest.DeviceId, secret, Environment.MachineName), token);
                }
                else
                {
                    await SendJsonAsync(socket, ProtocolMessages.PairResponseFailure("invalid or expired pairing code"), token);
                }
                return isAuthenticated;
            }
            case "AUTHENTICATE" when message.Authenticate is { } auth:
            {
                var secret = _trustedDevices.TryGetSecret(auth.DeviceId);
                var timestampFresh = DateTimeOffset.TryParse(auth.Timestamp, out var authTime) &&
                                      (DateTimeOffset.UtcNow - authTime).Duration() <= AuthTimestampWindow;
                var verified = secret is not null && timestampFresh &&
                                AuthProof.Verify(secret, auth.DeviceId, auth.Nonce, auth.Timestamp, auth.Proof);

                await SendJsonAsync(socket, ProtocolMessages.AuthResult(verified, verified ? null : "authentication failed"), token);
                return verified;
            }
            case "NOTIFICATION" or "NOTIFICATION_REMOVED":
                if (isAuthenticated)
                    AuthorizedNotification?.Invoke(rawJson);
                return isAuthenticated;
            default:
                return isAuthenticated;
        }
    }

    private static async Task SendJsonAsync(WebSocket socket, string json, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
    }

    private static async Task<string?> ReadWebSocketKeyAsync(Stream stream, CancellationToken token)
    {
        var headerBytes = new List<byte>();
        var buffer = new byte[1];
        while (headerBytes.Count < 32 * 1024)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, 1), token);
            if (read == 0) return null;
            headerBytes.Add(buffer[0]);

            if (headerBytes.Count >= 4 &&
                headerBytes[^4] == '\r' && headerBytes[^3] == '\n' &&
                headerBytes[^2] == '\r' && headerBytes[^1] == '\n')
                break;
        }

        var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
        foreach (var line in headerText.Split("\r\n"))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0) continue;

            var name = line[..separatorIndex].Trim();
            if (string.Equals(name, "Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                return line[(separatorIndex + 1)..].Trim();
        }

        return null;
    }

    private static async Task WriteUpgradeResponseAsync(Stream stream, string secWebSocketKey, CancellationToken token)
    {
        var acceptSource = secWebSocketKey + WebSocketMagicGuid;
        var acceptHash = SHA1.HashData(Encoding.ASCII.GetBytes(acceptSource));
        var acceptKey = Convert.ToBase64String(acceptHash);

        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        var responseBytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(responseBytes, token);
    }
}
