using System.Windows;
using NotificationBridge.Windows.Notifications;
using NotificationBridge.Windows.Protocol;
using NotificationBridge.Windows.Transport;
using NotificationBridge.Windows.UI;

namespace NotificationBridge.Windows;

public partial class MainWindow : Window
{
    private const int Port = 7787;
    private readonly LocalWebSocketReceiver _receiver = new(Port);
    private readonly NotificationManager _notificationManager = new();
    private readonly OverlayWindow _overlay = new();

    public MainWindow()
    {
        InitializeComponent();

        _notificationManager.NotificationAdded += n => _overlay.Add(n);
        _notificationManager.NotificationRemoved += id => _overlay.Remove(id);
        _overlay.Show();

        _receiver.ClientConnectionChanged += OnClientConnectionChanged;
        _receiver.MessageReceived += OnMessageReceived;
        _receiver.Start();

        StatusText.Text = $"Listening on ws://127.0.0.1:{Port}/ws/ (no client connected)";
        Closing += (_, _) =>
        {
            _receiver.Stop();
            _overlay.Close();
        };
    }

    private void OnClientConnectionChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = connected
                ? $"Listening on ws://127.0.0.1:{Port}/ws/ (client connected)"
                : $"Listening on ws://127.0.0.1:{Port}/ws/ (no client connected)";
        });
    }

    private void OnMessageReceived(string rawJson)
    {
        var result = ProtocolDecoder.Decode(rawJson);

        Dispatcher.Invoke(() =>
        {
            if (result.Status == DecodeStatus.Ok && result.Message is { } msg)
            {
                var summary = msg.Notification is { } n
                    ? $"{msg.MessageType} package={n.PackageName} title=\"{n.Title}\" body=\"{n.Body}\""
                    : msg.MessageType;
                LogList.Items.Add($"[OK] {summary}");
                _notificationManager.HandleDecoded(msg);
            }
            else
            {
                LogList.Items.Add($"[REJECTED:{result.Status}] {string.Join("; ", result.Errors)}");
            }

            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        });
    }

    private async void SendValidButton_Click(object sender, RoutedEventArgs e)
    {
        await SyntheticTestClient.SendAsync(Port, SyntheticMessages.ValidNotification());
    }

    private async void SendMalformedButton_Click(object sender, RoutedEventArgs e)
    {
        await SyntheticTestClient.SendAsync(Port, SyntheticMessages.MalformedNotification());
    }
}
