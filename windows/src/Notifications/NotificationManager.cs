using System.Windows.Threading;
using NotificationBridge.Windows.Protocol;
using NotificationBridge.Windows.Settings;

namespace NotificationBridge.Windows.Notifications;

// Each incoming NOTIFICATION message becomes its own independent bubble, keyed by the envelope's
// messageId, rather than deduping by Android's notificationId. That's a deliberate product choice
// (not ARCHITECTURE.md 4.3's original update-in-place default): Android reuses the same
// notificationId for every new message within one conversation, but the user wants each message
// to show as its own toast rather than collapsing to "latest message only." A NOTIFICATION_REMOVED
// signal (the phone-side conversation being read/cleared) is intentionally ignored for display
// purposes; each bubble just expires on its own independent timer.
//
// Assumes it is only ever called from the UI thread (matches how MainWindow invokes it from
// inside Dispatcher.Invoke), so it does not do its own locking.
public sealed class NotificationManager
{
    private readonly AppSettings _settings;
    private readonly Dictionary<string, DispatcherTimer> _timers = new();
    private readonly Queue<string> _arrivalOrder = new();

    public NotificationManager(AppSettings settings)
    {
        _settings = settings;
    }

    public event Action<VisibleNotification>? NotificationAdded;
    public event Action<string>? NotificationRemoved;

    public void HandleDecoded(DecodedMessage message)
    {
        if (message.MessageType != "NOTIFICATION" || message.Notification is not { } payload)
            return;

        while (_timers.Count >= _settings.MaxVisibleBubbles && _arrivalOrder.Count > 0)
            Remove(_arrivalOrder.Dequeue());

        var entry = new VisibleNotification
        {
            Id = message.MessageId,
            PackageName = payload.PackageName,
            AppName = payload.AppName,
            Title = payload.Title,
            Body = payload.Body,
            IconPng = payload.IconPng,
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.BubbleDurationSeconds) };
        timer.Tick += (_, _) => Remove(message.MessageId);
        timer.Start();

        _timers[message.MessageId] = timer;
        _arrivalOrder.Enqueue(message.MessageId);
        NotificationAdded?.Invoke(entry);
    }

    // Hover-to-pause (UI_UX.md section 1): the dismissal timer stops while the cursor is over a
    // bubble and restarts with the full duration once it leaves, so there is time to finish reading.
    public void Pause(string bubbleId)
    {
        if (_timers.TryGetValue(bubbleId, out var timer))
            timer.Stop();
    }

    public void Resume(string bubbleId)
    {
        if (_timers.TryGetValue(bubbleId, out var timer))
            timer.Start();
    }

    private void Remove(string bubbleId)
    {
        if (!_timers.TryGetValue(bubbleId, out var timer))
            return;

        timer.Stop();
        _timers.Remove(bubbleId);
        NotificationRemoved?.Invoke(bubbleId);
    }
}
