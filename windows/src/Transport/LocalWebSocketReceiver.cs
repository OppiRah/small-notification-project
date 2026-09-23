using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace NotificationBridge.Windows.Transport;

public sealed class LocalWebSocketReceiver
{
    private readonly int _port;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action<string>? MessageReceived;
    public event Action<bool>? ClientConnectionChanged;

    public LocalWebSocketReceiver(int port)
    {
        _port = port;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/ws/");
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync();
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested) return;
                continue;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            _ = HandleClientAsync(context, token);
        }
    }

    private async Task HandleClientAsync(HttpListenerContext context, CancellationToken token)
    {
        HttpListenerWebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        var socket = wsContext.WebSocket;
        ClientConnectionChanged?.Invoke(true);

        var buffer = new byte[16 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var messageStream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", token);
                        break;
                    }
                    messageStream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var text = Encoding.UTF8.GetString(messageStream.ToArray());
                MessageReceived?.Invoke(text);
            }
        }
        catch (Exception)
        {
            // Client dropped the connection; fall through to cleanup below.
        }
        finally
        {
            ClientConnectionChanged?.Invoke(false);
            socket.Dispose();
        }
    }
}
