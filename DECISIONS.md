# Architecture Decision Record

This file records important decisions and unresolved questions.

## ADR-001 — Local-first architecture

### Decision

Notification contents travel directly between the Android phone and Windows PC over the local network.

### Reason

The application is personal-use and notification contents are sensitive. A cloud intermediary adds infrastructure, latency, cost, and privacy exposure without solving the core problem.

### Consequence

Phone and PC must be able to communicate locally.

---

## ADR-002 — Android notification listener

### Decision

Use Android's `NotificationListenerService` as the notification acquisition mechanism.

### Reason

It is the platform API intended to receive notification events.

### Reference

https://developer.android.com/reference/kotlin/android/service/notification/NotificationListenerService

---

## ADR-003 — App-agnostic notification model

### Decision

Do not implement per-app integrations for the MVP.

### Reason

The desired behavior is "show whatever Android reports as a notification."

### Consequence

The extractor must handle multiple Android notification styles generically.

---

## ADR-004 — Windows desktop overlay

### Decision

Render notifications as a dedicated desktop overlay rather than using Windows' native notification system as the primary UI.

### Reason

The user needs:

- exact monitor selection
- exact placement
- custom bubble layout
- custom lifetime
- custom stacking
- notification content presentation

---

## ADR-005 — No notification database in MVP

### Decision

Do not persist notification bodies.

### Reason

The product is an ephemeral notification bridge. Persistence increases privacy risk and complexity without being necessary for the core workflow.

---

## ADR-006 — Windows UI framework: WPF (.NET)

### Decision

Build the Windows receiver/overlay with WPF (.NET).

### Reason

Mature, well-documented, and the most direct path to a transparent, click-through, always-on-top
overlay window plus a system tray icon. Ships as a plain executable with no MSIX packaging step,
which keeps the system easy to understand and debug per ARCHITECTURE.md's stated goal.

### Alternatives considered

- WinUI 3 / Windows App SDK — more modern, but MSIX packaging and undecorated transparent overlay
  windows are notably more involved to get working correctly.
- Avalonia — solid overlay support, but its cross-platform ability is unneeded on a Windows-only
  tool.

---

## ADR-007 — Transport: WebSocket over local network

### Decision

Use WebSocket over the local network as the protocol transport, per PROTOCOL.md's stated
candidate.

### Reason

Persistent bidirectional connection, simple message framing, natural fit for heartbeat/reconnect
logic, no polling.

---

## Unresolved decisions

The implementation team must explicitly decide:

- port/discovery strategy
- pairing mechanism
- authentication protocol
- encryption strategy
- Android minimum SDK
- Windows minimum version
- icon transfer format
- notification update/removal semantics
- whether ongoing notifications should be forwarded
- behavior for silent notifications
- maximum bubble count
- click behavior
- whether hover-to-pause belongs in MVP
