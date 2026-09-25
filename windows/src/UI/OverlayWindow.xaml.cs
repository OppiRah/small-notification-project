using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    private readonly AppSettings _settings;
    private readonly Dictionary<string, BubbleControl> _bubbles = new();

    // The overlay is click-through (WS_EX_TRANSPARENT), so it receives no mouse events. Hover is
    // detected by polling the cursor against each bubble's on-screen rectangle instead, which keeps
    // the overlay from ever intercepting a click meant for the window underneath.
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly HashSet<string> _hovered = new();

    public event Action<string, bool>? BubbleHoverChanged;

    public OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _hoverTimer.Tick += (_, _) => PollHover();
    }

    private void PollHover()
    {
        if (!GetCursorPos(out var cursor))
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        foreach (var (id, bubble) in _bubbles)
        {
            var topLeft = bubble.PointToScreen(new Point(0, 0));
            var over = cursor.X >= topLeft.X && cursor.X < topLeft.X + bubble.ActualWidth * dpi.DpiScaleX &&
                       cursor.Y >= topLeft.Y && cursor.Y < topLeft.Y + bubble.ActualHeight * dpi.DpiScaleY;

            if (over && _hovered.Add(id))
                BubbleHoverChanged?.Invoke(id, true);
            else if (!over && _hovered.Remove(id))
                BubbleHoverChanged?.Invoke(id, false);
        }
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
        var bubble = new BubbleControl(notification.Id, notification.AppName, notification.Title, notification.Body, notification.IconPng, _settings.AnimationEnabled);
        _bubbles[notification.Id] = bubble;

        var isTop = _settings.Corner is BubbleCorner.TopLeft or BubbleCorner.TopRight;
        if (isTop)
            BubbleStack.Children.Insert(0, bubble);
        else
            BubbleStack.Children.Add(bubble);

        _hoverTimer.Start();
    }

    public void Remove(string id)
    {
        if (!_bubbles.TryGetValue(id, out var bubble))
            return;

        _bubbles.Remove(id);
        _hovered.Remove(id);
        if (_bubbles.Count == 0)
            _hoverTimer.Stop();
        bubble.FadeOutAndRemove(_settings.AnimationEnabled, () => BubbleStack.Children.Remove(bubble));
    }
}
