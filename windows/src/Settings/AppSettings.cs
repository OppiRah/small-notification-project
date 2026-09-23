namespace NotificationBridge.Windows.Settings;

public enum BubbleCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public sealed class AppSettings
{
    public int MonitorIndex { get; set; }
    public BubbleCorner Corner { get; set; } = BubbleCorner.TopRight;
    public int BubbleDurationSeconds { get; set; } = 6;
    public int MaxVisibleBubbles { get; set; } = 5;
    public bool AnimationEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
}
