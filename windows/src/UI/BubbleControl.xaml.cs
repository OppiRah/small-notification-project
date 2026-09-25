using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NotificationBridge.Windows.UI;

public partial class BubbleControl : UserControl
{
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromMilliseconds(200);
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    private readonly TranslateTransform _offset = new();

    public string NotificationId { get; }

    // slideOffset is the horizontal distance (in DIPs) the bubble slides in from; 0 means fade only.
    public BubbleControl(string notificationId, string? appName, string? title, string? body, byte[]? iconPng, bool animate, double slideOffset)
    {
        InitializeComponent();
        NotificationId = notificationId;
        RenderTransform = _offset;
        UpdateContent(appName, title, body);
        ShowIcon(iconPng);

        if (SystemParameters.HighContrast)
            ApplyHighContrast();

        if (animate)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, EnterDuration) { EasingFunction = EaseOut });
            if (slideOffset != 0)
                _offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(slideOffset, 0, EnterDuration) { EasingFunction = EaseOut });
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

    // Windows high-contrast mode: use the user's system colors and drop the translucent card and
    // shadow so text always has the contrast the user configured.
    private void ApplyHighContrast()
    {
        Card.Background = SystemColors.WindowBrush;
        Card.BorderBrush = SystemColors.WindowTextBrush;
        Card.BorderThickness = new Thickness(2);
        Card.Effect = null;
        HeaderText.Foreground = TitleText.Foreground = BodyText.Foreground = SystemColors.WindowTextBrush;
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

    // Called after the stack reflows: the bubble's layout position changed by -deltaY, so start it
    // visually where it was and ease it to its new spot. Adding the current offset keeps an
    // in-flight shift continuous instead of snapping.
    public void AnimateShift(double deltaY)
    {
        _offset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(deltaY + _offset.Y, 0, ShiftDuration) { EasingFunction = EaseOut });
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
