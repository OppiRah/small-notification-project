using System.Runtime.InteropServices;

namespace NotificationBridge.Windows.Display;

public readonly record struct MonitorArea(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

// Monitor selection UI is Phase 6; for now this always targets the primary monitor, but returns
// real working-area bounds via Win32's GetMonitorInfo (per ARCHITECTURE.md 4.4) rather than
// hardcoding dimensions, so a monitor placed left of/above the primary won't break placement math
// later, per UI_UX.md section 2.
public static class DisplayManager
{
    private const uint MonitorDefaultToPrimary = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    public static MonitorArea GetTargetWorkingArea()
    {
        var monitor = MonitorFromPoint(new Point { X = 0, Y = 0 }, MonitorDefaultToPrimary);
        var info = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return new MonitorArea(0, 0, 1920, 1040);

        return new MonitorArea(info.RcWork.Left, info.RcWork.Top, info.RcWork.Right, info.RcWork.Bottom);
    }
}
