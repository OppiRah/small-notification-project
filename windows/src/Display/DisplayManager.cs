using System.Runtime.InteropServices;

namespace NotificationBridge.Windows.Display;

public readonly record struct MonitorArea(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record MonitorDescriptor(int Index, MonitorArea WorkingArea, bool IsPrimary)
{
    public override string ToString() => IsPrimary ? $"Monitor {Index + 1} (Primary)" : $"Monitor {Index + 1}";
}

// Enumerates real monitors via Win32's EnumDisplayMonitors/GetMonitorInfo (per ARCHITECTURE.md
// 4.4) rather than assuming a single primary display, so Phase 6's monitor picker has real
// options and placement math never assumes positive coordinates, per UI_UX.md section 2.
public static class DisplayManager
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }

    private const uint MonitorInfoFPrimary = 0x00000001;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    public static IReadOnlyList<MonitorDescriptor> GetAllMonitors()
    {
        var monitors = new List<MonitorDescriptor>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            var info = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var area = new MonitorArea(info.RcWork.Left, info.RcWork.Top, info.RcWork.Right, info.RcWork.Bottom);
                var isPrimary = (info.DwFlags & MonitorInfoFPrimary) != 0;
                monitors.Add(new MonitorDescriptor(monitors.Count, area, isPrimary));
            }
            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        if (monitors.Count == 0)
            monitors.Add(new MonitorDescriptor(0, new MonitorArea(0, 0, 1920, 1040), true));

        return monitors;
    }

    public static MonitorArea GetWorkingArea(int monitorIndex)
    {
        var monitors = GetAllMonitors();
        if (monitorIndex >= 0 && monitorIndex < monitors.Count)
            return monitors[monitorIndex].WorkingArea;

        foreach (var monitor in monitors)
        {
            if (monitor.IsPrimary)
                return monitor.WorkingArea;
        }

        return monitors[0].WorkingArea;
    }
}
