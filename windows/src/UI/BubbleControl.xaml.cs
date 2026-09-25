using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NotificationBridge.Windows.UI;

public partial class BubbleControl : UserControl
{
    public string NotificationId { get; }

    public BubbleControl(string notificationId, string? appName, string? title, string? body, byte[]? iconPng, bool animate)
    {
        InitializeComponent();
        NotificationId = notificationId;
        UpdateContent(appName, title, body);
        ShowIcon(iconPng);

        if (animate)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }
        else
        {
            Opacity = 1;
        }
    }

    private void UpdateContent(string? appName, string? title, string? body)
    {
        HeaderText.Text = appName ?? "";
        TitleText.Text = title ?? "";
        BodyText.Text = body ?? "";
        TitleText.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
        BodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowIcon(byte[]? iconPng)
    {
        if (iconPng is null)
            return;

        try
        {
            using var stream = new MemoryStream(iconPng);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            IconImage.Source = image;
            IconImage.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            // Bytes came off the network. A bad icon just means the bubble shows without one.
        }
    }

    public void FadeOutAndRemove(bool animate, Action onComplete)
    {
        if (!animate)
        {
            onComplete();
            return;
        }

        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
        animation.Completed += (_, _) => onComplete();
        BeginAnimation(OpacityProperty, animation);
    }
}
