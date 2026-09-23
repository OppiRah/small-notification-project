using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NotificationBridge.Windows.UI;

public partial class BubbleControl : UserControl
{
    public string NotificationId { get; }

    public BubbleControl(string notificationId, string? appName, string? title, string? body)
    {
        InitializeComponent();
        NotificationId = notificationId;
        UpdateContent(appName, title, body);

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    private void UpdateContent(string? appName, string? title, string? body)
    {
        HeaderText.Text = appName ?? "";
        TitleText.Text = title ?? "";
        BodyText.Text = body ?? "";
        TitleText.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
        BodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void FadeOutAndRemove(Action onComplete)
    {
        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
        animation.Completed += (_, _) => onComplete();
        BeginAnimation(OpacityProperty, animation);
    }
}
