# System Architecture

## 1. Architectural goal

Build a small, event-driven, local-first system with a strict separation between:

- Android notification acquisition
- notification normalization
- transport
- connection/authentication
- desktop notification state
- desktop rendering
- configuration

The system should remain understandable to a CS student and easy to debug.

---

## 2. High-level architecture

```text
┌────────────────────────────────────────────────────────────┐
│                         ANDROID                            │
│                                                            │
│  Android Notification System                               │
│             │                                              │
│             ▼                                              │
│  NotificationListenerService                              │
│             │                                              │
│             ▼                                              │
│  Notification Extractor / Normalizer                      │
│             │                                              │
│             ▼                                              │
│  Local Transport Client                                    │
│             │                                              │
└─────────────┼──────────────────────────────────────────────┘
              │
              │ authenticated local connection
              │
┌─────────────┼──────────────────────────────────────────────┐
│             ▼                 WINDOWS                       │
│  Connection Manager                                          │
│             │                                                │
│             ▼                                                │
│  Protocol Decoder / Validator                                │
│             │                                                │
│             ▼                                                │
│  Notification Manager                                        │
│      │           │                                           │
│      │           └── lifecycle/timers                        │
│      │                                                       │
│      ▼                                                       │
│  Display/Placement Manager                                  │
│             │                                                │
│             ▼                                                │
│  Bubble Renderer                                             │
│             │                                                │
│             ▼                                                │
│       Desktop Overlay                                        │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. Android components

### 3.1 NotificationListenerService

Responsibilities:

- receive notification callbacks
- detect notification creation/update/removal
- obtain the Android notification object
- pass raw notification data to an extractor

Do not put networking, UI, persistence, and notification parsing into this class.

The Android documentation identifies `NotificationListenerService` as the platform mechanism for receiving callbacks when notifications are posted/removed/ranking changes. The service must declare the appropriate notification-listener service permission and should wait for `onListenerConnected()` before operating.

Reference:
https://developer.android.com/reference/kotlin/android/service/notification/NotificationListenerService

---

### 3.2 NotificationExtractor

Responsibilities:

- read notification fields
- handle different notification styles
- normalize notification content
- safely handle missing fields
- avoid assumptions about individual applications

The extractor should produce a project-owned domain object.

Example:

```text
NormalizedNotification
├── id
├── packageName
├── appName
├── title
├── body
├── expandedLines[]
├── summary
├── timestamp
├── category
├── icon
└── metadata
```

---

### 3.3 TransportClient

Responsibilities:

- connect to the paired Windows endpoint
- authenticate
- send protocol messages
- detect disconnects
- reconnect
- apply backoff
- expose connection state

It should not know how notification bubbles are rendered.

---

## 4. Windows components

### 4.1 ConnectionManager

Responsibilities:

- start/stop transport
- maintain connection state
- reconnect
- authenticate
- validate remote device
- expose connection events

Suggested state machine:

```text
DISCONNECTED
    ↓
CONNECTING
    ↓
AUTHENTICATING
    ↓
CONNECTED
    ↓
RECONNECTING
    ↓
CONNECTING
```

Error conditions should not create infinite rapid reconnect loops.

Use bounded exponential backoff with jitter.

---

### 4.2 ProtocolDecoder

Responsibilities:

- parse wire messages
- validate schema/version
- reject malformed data
- enforce reasonable field-size limits
- convert wire objects into internal domain objects

The UI must never directly consume unvalidated network JSON.

---

### 4.3 NotificationManager

Responsibilities:

- receive validated notifications
- deduplicate/update where appropriate
- assign lifecycle state
- manage visible notifications
- calculate stack positions
- start/stop timers
- notify renderer

Possible state:

```text
RECEIVED
VISIBLE
PAUSED
EXPIRING
REMOVED
```

---

### 4.4 DisplayManager

Responsibilities:

- enumerate monitors
- identify primary monitor
- identify configured target monitor
- retrieve monitor work area
- react to monitor configuration changes
- calculate safe overlay coordinates

Windows provides APIs such as `EnumDisplayMonitors` and `GetMonitorInfo` for monitor enumeration and monitor/work-area information.

Do not calculate positions using only primary-monitor dimensions.

Reference:
https://learn.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors

---

### 4.5 BubbleRenderer

Responsibilities:

- render notification card
- animate enter/exit
- display icon/text
- maintain transparency
- remain above normal windows
- avoid stealing focus
- handle mouse interaction
- update stack positions

Rendering implementation should be isolated from notification business logic.

---

## 5. Window behavior

The overlay is a desktop utility window, not a normal application window.

Desired properties:

- borderless
- transparent background
- topmost
- non-activating where practical
- no taskbar presence for individual bubbles
- click-through by default if interaction is not needed
- configurable mouse interaction if hover-to-pause is implemented

Windows supports always-on-top window behavior, but topmost does not guarantee placement above other topmost windows. This limitation should be understood and documented.

Reference:
https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.overlappedpresenter.isalwaysontop

---

## 6. Protocol architecture

Use an explicit versioned protocol.

Example envelope:

```json
{
  "protocolVersion": 1,
  "messageType": "notification",
  "messageId": "uuid",
  "sentAt": "2026-09-20T10:00:00Z",
  "payload": {
    "notificationId": "example",
    "packageName": "com.example.app",
    "appName": "Example",
    "title": "John",
    "body": "Hello",
    "expandedLines": [],
    "timestamp": "2026-09-20T09:59:58Z"
  }
}
```

Do not treat this example as the final schema.

The implementation team must define:

- required fields
- optional fields
- maximum lengths
- supported types
- versioning policy
- error messages
- authentication messages
- heartbeat messages

---

## 7. Message types

Initial protocol should support at least:

```text
HELLO
PAIR_REQUEST
PAIR_RESPONSE
AUTHENTICATE
AUTH_RESULT
HEARTBEAT
NOTIFICATION
NOTIFICATION_REMOVED
ACK
ERROR
```

The exact set can be reduced if the implementation discovers that some are unnecessary.

Do not add protocol messages simply because they sound architecturally complete.

---

## 8. Notification delivery semantics

The MVP should favor:

> **at-most-once display delivery**

Reason:

A duplicate bubble is annoying but usually less harmful than a complex persistence/replay system.

However, transport-level reliability should still be used so that ordinary packet loss is handled by the chosen protocol.

If the implementation uses a reliable stream, message framing and duplicate handling still need to be considered.

---

## 9. Ordering

Notifications should normally be displayed in arrival order.

Each notification carries its originating timestamp where available.

The PC should not block newer notifications because an older notification is still visible.

---

## 10. Duplicate handling

Android can update an existing notification.

The system must distinguish:

```text
new notification
```

from:

```text
existing notification updated
```

The exact Android notification key/identity strategy must be researched during implementation.

Potential behavior:

- new notification → create bubble
- update to currently visible notification → update bubble
- removed notification → optionally remove its bubble

Do not assume package name alone uniquely identifies a notification.

---

## 11. Security architecture

### Pairing

Recommended initial flow:

```text
PC starts
  ↓
PC generates pairing challenge/code
  ↓
Phone discovers or receives PC endpoint
  ↓
User confirms matching code
  ↓
Devices establish trust
  ↓
PC stores trusted device identity
```

The exact cryptographic design should be reviewed before implementation.

Avoid inventing cryptography.

Use established primitives and platform libraries.

---

## 12. Local network assumptions

MVP assumes:

- phone and PC can communicate over a local network
- both devices can reach the chosen endpoint
- Windows firewall may need a rule
- Android network/security policies must permit the chosen transport

The implementation should provide clear diagnostics when connection fails.

---

## 13. Persistence

MVP persistence should be minimal.

Persist:

- pairing/trust information
- user settings

Do not persist:

- notification body
- notification history
- notification images

unless a later feature explicitly requires it.

---

## 14. Suggested repository structure

```text
phone-notification-bridge/
│
├── README.md
├── PRODUCT.md
├── REQUIREMENTS.md
├── UI_UX.md
├── ARCHITECTURE.md
├── PROTOCOL.md
├── SECURITY.md
├── ROADMAP.md
├── TESTING.md
├── DEVELOPMENT.md
├── DECISIONS.md
│
├── android/
│   ├── app/
│   ├── gradle/
│   └── ...
│
├── windows/
│   ├── src/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   ├── Transport/
│   │   ├── Notifications/
│   │   ├── Display/
│   │   └── UI/
│   ├── tests/
│   └── ...
│
└── docs/
```

The exact project layout depends on the selected Windows framework and Android architecture.

---

## 15. Architecture rules

1. UI does not parse network packets.
2. Network layer does not render UI.
3. Android listener does not own networking implementation.
4. Notification extraction is independent from transport.
5. Domain models are independent from UI framework types.
6. Settings are not scattered throughout the application.
7. Logging must respect privacy.
8. Network input is untrusted input.
9. Timers/lifecycle must have explicit ownership.
10. Monitor calculations belong to display/window management, not notification parsing.
