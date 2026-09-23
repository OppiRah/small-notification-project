using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NotificationBridge.Windows.Display;
using NotificationBridge.Windows.Notifications;
using NotificationBridge.Windows.Settings;

namespace NotificationBridge.Windows.UI;

// Borderless, transparent, always-on-top, non-activating, click-through overlay per
// ARCHITECTURE.md section 5. Positioned with SetWindowPos in physical pixels (not WPF's
// device-independent Left/Top/Width/Height) so placement is correct regardless of the target
// monitor's DPI scale factor.
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private const int OverlayWidthPixels = 340;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private readonly AppSettings _settings;
    private readonly Dictionary<string, BubbleControl> _bubbles = new();

    public OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate | WsExToolWindow | WsExTransparent | WsExLayered);

        ApplySettings();
    }

    public void ApplySettings()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var workingArea = DisplayManager.GetWorkingArea(_settings.MonitorIndex);
        var isLeft = _settings.Corner is BubbleCorner.TopLeft or BubbleCorner.BottomLeft;
        var x = isLeft ? workingArea.Left : workingArea.Right - OverlayWidthPixels;
        var y = workingArea.Top;
        SetWindowPos(hwnd, HwndTopmost, x, y, OverlayWidthPixels, workingArea.Height, SwpNoActivate | SwpShowWindow);

        var isTop = _settings.Corner is BubbleCorner.TopLeft or BubbleCorner.TopRight;
        BubbleStack.VerticalAlignment = isTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
    }

    public void Add(VisibleNotification notification)
    {
        var bubble = new BubbleControl(notification.Id, notification.AppName, notification.Title, notification.Body, _settings.AnimationEnabled);
        _bubbles[notification.Id] = bubble;

        var isTop = _settings.Corner is BubbleCorner.TopLeft or BubbleCorner.TopRight;
        if (isTop)
            BubbleStack.Children.Insert(0, bubble);
        else
            BubbleStack.Children.Add(bubble);
    }

    public void Remove(string id)
    {
        if (!_bubbles.TryGetValue(id, out var bubble))
            return;

        _bubbles.Remove(id);
        bubble.FadeOutAndRemove(_settings.AnimationEnabled, () => BubbleStack.Children.Remove(bubble));
    }
}
