using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using NotificationBridge.Windows.Notifications;
using NotificationBridge.Windows.Protocol;
using NotificationBridge.Windows.Security;
using NotificationBridge.Windows.Transport;
using NotificationBridge.Windows.UI;

namespace NotificationBridge.Windows;

public partial class MainWindow : Window
{
    private const int Port = 7787;
    private readonly PairingSession _pairingSession = new();
    private readonly TrustedDeviceStore _trustedDevices = new();
    private readonly LocalWebSocketReceiver _receiver;
    private readonly NotificationManager _notificationManager = new();
    private readonly OverlayWindow _overlay = new();
    private readonly DispatcherTimer _pairingStatusTimer;

    public MainWindow()
    {
        InitializeComponent();

        _receiver = new LocalWebSocketReceiver(Port, CertificateStore.LoadOrCreate(), _pairingSession, _trustedDevices);

        _notificationManager.NotificationAdded += n => _overlay.Add(n);
        _notificationManager.NotificationRemoved += id => _overlay.Remove(id);
        _overlay.Show();

        _receiver.ClientConnectionChanged += OnClientConnectionChanged;
        _receiver.MessageReceived += OnMessageReceived;
        _receiver.AuthorizedNotification += OnAuthorizedNotification;
        _receiver.Start();

        _pairingStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pairingStatusTimer.Tick += (_, _) => RefreshPairingStatus();

        RefreshTrustedDevicesList();
        StatusText.Text = $"Listening on wss://127.0.0.1:{Port}/ws/ (no client connected)";
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
                ? $"Listening on wss://127.0.0.1:{Port}/ws/ (client connected)"
                : $"Listening on wss://127.0.0.1:{Port}/ws/ (no client connected)";
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
            }
            else
            {
                LogList.Items.Add($"[REJECTED:{result.Status}] {string.Join("; ", result.Errors)}");
            }

            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);

            RefreshTrustedDevicesList();
        });
    }

    private void OnAuthorizedNotification(string rawJson)
    {
        var result = ProtocolDecoder.Decode(rawJson);
        if (result.Status != DecodeStatus.Ok || result.Message is not { } msg)
            return;

        Dispatcher.Invoke(() => _notificationManager.HandleDecoded(msg));
    }

    private async void SendValidButton_Click(object sender, RoutedEventArgs e)
    {
        await SyntheticTestClient.SendAsync(Port, SyntheticMessages.ValidNotification());
    }

    private async void SendMalformedButton_Click(object sender, RoutedEventArgs e)
    {
        await SyntheticTestClient.SendAsync(Port, SyntheticMessages.MalformedNotification());
    }

    private void PairButton_Click(object sender, RoutedEventArgs e)
    {
        var code = _pairingSession.Start();
        var ip = GetLocalIPv4() ?? "<unknown IP>";
        PairingStatusText.Text = $"Pairing code: {code}  |  PC address: {ip}:{Port}  |  expires in 2:00";
        _pairingStatusTimer.Start();
    }

    private void UnpairButton_Click(object sender, RoutedEventArgs e)
    {
        if (TrustedDevicesCombo.SelectedItem is TrustedDevice device)
        {
            _trustedDevices.Remove(device.DeviceId);
            RefreshTrustedDevicesList();
        }
    }

    private void RefreshPairingStatus()
    {
        if (!_pairingSession.IsActive)
        {
            PairingStatusText.Text = "";
            _pairingStatusTimer.Stop();
            return;
        }

        var remaining = _pairingSession.ExpiresAt - DateTimeOffset.UtcNow;
        var ip = GetLocalIPv4() ?? "<unknown IP>";
        PairingStatusText.Text =
            $"Pairing code: {_pairingSession.CurrentCode}  |  PC address: {ip}:{Port}  |  expires in {remaining:m\\:ss}";
        RefreshTrustedDevicesList();
    }

    private void RefreshTrustedDevicesList()
    {
        var selected = (TrustedDevicesCombo.SelectedItem as TrustedDevice)?.DeviceId;
        TrustedDevicesCombo.ItemsSource = null;
        var devices = _trustedDevices.List();
        TrustedDevicesCombo.ItemsSource = devices;
        TrustedDevicesCombo.DisplayMemberPath = nameof(TrustedDevice.DeviceName);
        TrustedDevicesCombo.SelectedItem = devices.FirstOrDefault(d => d.DeviceId == selected);
    }

    // Prefers an adapter with a real gateway configured (an actual network connection) over
    // virtual/isolated adapters like VirtualBox's host-only network, which has no gateway and
    // would otherwise get picked first by a naive "first non-loopback IPv4" scan.
    private static string? GetLocalIPv4()
    {
        var candidates = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                or System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
            .ToList();

        foreach (var nic in candidates.Where(nic => nic.GetIPProperties().GatewayAddresses.Count > 0))
        {
            var address = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            if (address is not null)
                return address.Address.ToString();
        }

        foreach (var nic in candidates)
        {
            var address = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            if (address is not null)
                return address.Address.ToString();
        }

        return null;
    }
}
