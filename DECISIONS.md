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

## Unresolved decisions

The implementation team must explicitly decide:

- Windows UI framework
- transport
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
