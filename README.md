# Phone Notification Bridge

## Project purpose

A personal-use Android → Windows notification bridge.

The phone is the **only source of notifications**. When Android receives a notification, the companion Windows application receives the notification over a local connection and renders it as a temporary, rich desktop bubble.

The core user experience is:

> **Phone notification → local connection → PC → unobtrusive desktop bubble → automatic dismissal**

The project exists to remove the repeated need to look away from the monitor, pick up the phone, unlock it, and expand a notification merely to see what it says.

This is intentionally **not** a phone-control platform, AI assistant, cloud service, or general remote-control system.

---

## Product definition

### Primary requirements

- Android phone receives notifications normally.
- A companion Android component observes notifications using Android's notification-listener facilities.
- Notification data is normalized into a project-defined protocol.
- Data is sent to the user's Windows PC over a local connection.
- Windows displays the notification as a temporary overlay.
- The overlay is above normal desktop applications.
- Notifications default to the primary monitor.
- The user can select another monitor.
- The user can configure placement, timing, and related display behavior.
- Multiple notifications can coexist/stack without replacing each other.
- Each notification has its own lifecycle/timer.
- The desktop should show as much notification content as Android exposes, rather than intentionally reproducing only the collapsed preview.
- If the phone is disconnected, the desktop bridge becomes idle and clearly indicates the disconnected state.
- No cloud backend is required.

### Non-goals for MVP

Do not add these merely because they are technically possible:

- replying to messages
- controlling phone apps
- SMS sending
- phone screen mirroring
- remote phone control
- AI summarization
- cloud synchronization
- account system
- public multi-user infrastructure
- cross-platform support
- arbitrary notification generation from the PC

These can be future experiments only after the notification pipeline is reliable.

---

## Important product principle

The project should be designed around the **notification abstraction**, not individual apps.

The desktop should not contain special cases such as:

```text
if Messenger...
if Discord...
if Instagram...
```

Instead, Android produces a normalized notification object:

```text
Notification
├── stable/local notification identity
├── package/application identity
├── application display name
├── title
├── body/content
├── expanded/structured content where available
├── timestamp
├── icon data where practical
├── category/type where useful
├── conversation/messaging information where available
└── optional metadata
```

An unknown Android application should work without changing the Windows application.

---

## Technology direction

Recommended initial stack:

### Android

- Kotlin
- Android Studio
- `NotificationListenerService`
- Android notification APIs
- Local networking
- JSON or another explicit, versioned wire format

### Windows

The implementation team should evaluate the simplest maintainable Windows desktop stack before committing.

Reasonable candidates:

- C# + WinUI 3 / Windows App SDK
- C# + WPF
- C++/Win32 if native control is genuinely needed

For a personal Windows utility, do **not** choose C++ merely because it is lower-level. The overlay, settings UI, networking, and maintainability matter more than theoretical native performance.

### Transport

Prefer a simple local-network transport initially:

- WebSocket over LAN is a strong candidate.
- A small TCP protocol is also possible.
- HTTP polling is not appropriate for real-time notification delivery.

The final choice must document:
- why it was selected,
- how pairing/authentication works,
- how reconnect works,
- how messages are framed,
- how protocol versions are handled.

---

## Security baseline

Notification contents are private data.

Even though this is a personal LAN utility:

- Do not expose an unauthenticated notification receiver.
- Do not send notification data to a third-party cloud service.
- Do not log full notification bodies by default.
- Do not persist notification content unless the user explicitly enables a history feature.
- Pair the phone and PC.
- Authenticate subsequent connections.
- Reject unknown devices.
- Avoid binding a service to all interfaces unless required.
- Document what network interfaces/ports are used.
- Consider encryption if the selected transport makes it practical.

A LAN is not automatically a trusted environment.

---

## Source-of-truth rule

Android owns notification truth.

The Windows side should not invent, reconstruct, or poll for phone notifications.

The flow is:

```text
Android notification system
        ↓
NotificationListenerService
        ↓
Notification normalization
        ↓
Transport
        ↓
Windows receiver
        ↓
Notification queue/state
        ↓
Overlay renderer
```

---

## MVP success criteria

The MVP is successful when:

1. The Android app is paired with the Windows app.
2. A notification arrives on the phone.
3. The Android listener receives it.
4. The notification is normalized.
5. The notification reaches Windows within a reasonable local-network latency.
6. Windows displays it on the configured monitor.
7. The bubble shows the available useful content.
8. Multiple notifications stack correctly.
9. Each bubble expires independently.
10. Disconnect/reconnect behavior is reliable.
11. The system does not steal focus from the application being used.
12. The overlay does not become a persistent nuisance.
13. The application survives ordinary network interruptions.
14. The system does not require a cloud service.

---

## Engineering standard

This is a learning project, but it should still be engineered like a small real utility.

Prioritize:

1. correctness
2. reliability
3. privacy/security
4. simplicity
5. maintainability
6. testability
7. usability
8. visual polish

Do not add architecture solely to make the repository look sophisticated.

Every abstraction should have a reason.
