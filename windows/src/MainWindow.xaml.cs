using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using NotificationBridge.Windows.Display;
using NotificationBridge.Windows.Notifications;
using NotificationBridge.Windows.Protocol;
using NotificationBridge.Windows.Security;
using NotificationBridge.Windows.Settings;
using NotificationBridge.Windows.Transport;
using NotificationBridge.Windows.UI;

namespace NotificationBridge.Windows;

public partial class MainWindow : Window
{
    private const int Port = 7787;
    private const int MaxLogEntries = 500;
    private readonly AppSettings _settings;
    private readonly PairingSession _pairingSession = new();
    private readonly TrustedDeviceStore _trustedDevices = new();
    private readonly LocalWebSocketReceiver _receiver;
    private readonly NotificationManager _notificationManager;
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _pairingStatusTimer;
    private bool _clientConnected;

    public MainWindow()
    {
        InitializeComponent();

        _settings = SettingsStore.Load();
        _settings.StartWithWindows = StartupManager.IsEnabled();

        _notificationManager = new NotificationManager(_settings);
        _overlay = new OverlayWindow(_settings);
        _receiver = new LocalWebSocketReceiver(Port, CertificateStore.LoadOrCreate(), _pairingSession, _trustedDevices);

        _notificationManager.NotificationAdded += n => _overlay.Add(n);
        _notificationManager.NotificationRemoved += id => _overlay.Remove(id);
        _overlay.BubbleHoverChanged += (id, hovered) =>
        {
            if (hovered) _notificationManager.Pause(id);
            else _notificationManager.Resume(id);
        };
        _overlay.Show();

        _receiver.ClientConnectionChanged += OnClientConnectionChanged;
        _receiver.MessageReceived += OnMessageReceived;
        _receiver.AuthorizedNotification += OnAuthorizedNotification;
        _receiver.Start();

        _pairingStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pairingStatusTimer.Tick += (_, _) => RefreshPairingStatus();

        PopulateSettingsControls();
        RefreshTrustedDevicesList();
        UpdateStatusText(connected: false);

        // Both events fire on background threads. Monitor unplug/rearrange must re-place the
        // overlay (GetWorkingArea falls back to the primary monitor if the chosen one is gone);
        // a Wi-Fi change or wake from sleep can change the PC's IP shown in the status text.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

        Closing += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            _receiver.Stop();
            _overlay.Close();
        };
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RefreshMonitorList();
            _overlay.ApplySettings();
        });
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => UpdateStatusText(_clientConnected));
    }

    private void RefreshMonitorList()
    {
        var monitors = DisplayManager.GetAllMonitors();
        MonitorCombo.ItemsSource = monitors;
        MonitorCombo.SelectedIndex = _settings.MonitorIndex >= 0 && _settings.MonitorIndex < monitors.Count
            ? _settings.MonitorIndex
            : 0;
    }

    private void PopulateSettingsControls()
    {
        RefreshMonitorList();

        CornerCombo.SelectedIndex = (int)_settings.Corner;

        DurationSlider.Value = _settings.BubbleDurationSeconds;
        DurationValueText.Text = $"{_settings.BubbleDurationSeconds}s";

        MaxBubblesSlider.Value = _settings.MaxVisibleBubbles;
        MaxBubblesValueText.Text = _settings.MaxVisibleBubbles.ToString();

        AnimationCheckBox.IsChecked = _settings.AnimationEnabled;
        StartupCheckBox.IsChecked = _settings.StartWithWindows;
    }

    private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.MonitorIndex = MonitorCombo.SelectedIndex >= 0 ? MonitorCombo.SelectedIndex : 0;
        _settings.Corner = (BubbleCorner)CornerCombo.SelectedIndex;
        _settings.BubbleDurationSeconds = (int)DurationSlider.Value;
        _settings.MaxVisibleBubbles = (int)MaxBubblesSlider.Value;
        _settings.AnimationEnabled = AnimationCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartupCheckBox.IsChecked == true;

        SettingsStore.Save(_settings);
        StartupManager.SetEnabled(_settings.StartWithWindows);
        _overlay.ApplySettings();
    }

    private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DurationValueText is not null)
            DurationValueText.Text = $"{(int)e.NewValue}s";
    }

    private void MaxBubblesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxBubblesValueText is not null)
            MaxBubblesValueText.Text = ((int)e.NewValue).ToString();
    }

    private void OnClientConnectionChanged(bool connected)
    {
        Dispatcher.Invoke(() => UpdateStatusText(connected));
    }

    private void UpdateStatusText(bool connected)
    {
        _clientConnected = connected;
        var ip = GetLocalIPv4() ?? "0.0.0.0";
        StatusText.Text = connected
            ? $"Listening on wss://{ip}:{Port}/ws/ (client connected)"
            : $"Listening on wss://{ip}:{Port}/ws/ (no client connected)";
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

            // The Developer log would otherwise grow for as long as the app stays running.
            while (LogList.Items.Count > MaxLogEntries)
                LogList.Items.RemoveAt(0);

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
