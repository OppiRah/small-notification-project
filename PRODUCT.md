# Product — Exact User Experience

This doc describes what the user experiences: the problem, the flow, and how the system behaves
in normal and degraded conditions. For the underlying must/should/must-not requirements, see
`REQUIREMENTS.md`. For exact bubble/monitor/placement visual behavior, see `UI_UX.md`.

---

## 1. Problem

The user primarily works on a laptop with an additional external monitor.

When the phone vibrates or displays a notification, the user currently has to:

1. stop looking at the monitor,
2. pick up the phone,
3. wake/unlock it,
4. inspect the notification,
5. sometimes expand the notification to see its full contents,
6. return attention to the computer.

The desired system removes that interruption for ordinary notifications.

---

## 2. Product statement

**Phone Notification Bridge** mirrors Android notifications to a Windows desktop as temporary,
non-intrusive bubbles.

The user should be able to understand:

- which app generated the notification,
- who/what generated it when available,
- what the notification says,
- and enough expanded notification content to avoid unnecessarily checking the phone.

---

## 3. User experience

### Normal notification

```text
Phone
  ↓
Notification arrives
  ↓
PC receives notification
  ↓
Bubble appears
  ↓
Bubble remains visible for configured duration
  ↓
Bubble fades
  ↓
Bubble disappears
```

### Multiple notifications

```text
Notification A
Notification B
Notification C
```

The notification manager owns the stack.

Each notification has its own timer.

Removing A must not reset B or C.

---

## 4. Connection states

The desktop UI should distinguish:

```text
DISCONNECTED
CONNECTING
PAIRING
CONNECTED
RECONNECTING
ERROR
```

The overlay system should not generate fake notification bubbles for connection-state changes.

Connection state belongs in the application/status UI, not the bubble stack.

---

## 5. Failure behavior

### Phone offline

Windows:

```text
Connected → Reconnecting → Disconnected
```

No crash.

### Wi-Fi interruption

The system should reconnect automatically.

### PC sleep/wake

The connection should recover.

### Phone app process/service disruption

The system should recover where Android permits it.

### Windows application restart

The user should not have to manually pair again if the device is already trusted.

### Malformed packet

Reject the packet safely.

Do not crash the notification renderer.
