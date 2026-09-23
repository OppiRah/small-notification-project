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

## ADR-008 — Per-message bubbles instead of per-conversation update-in-place

### Decision

Every incoming NOTIFICATION message renders as its own independent bubble, keyed by the wire
envelope's messageId. NOTIFICATION_REMOVED is ignored for display purposes; a bubble only ever
disappears via its own expiry timer.

### Reason

Android reuses the same notificationId for every new message within one conversation, so
ARCHITECTURE.md 4.3's original "update to currently visible notification -> update bubble in
place" default would collapse a fast-moving conversation down to "latest message only" and hide
everything said in between. The user explicitly wants a running message log instead: each message
gets its own bubble, stacking newest-on-top, each expiring independently. Ignoring
NOTIFICATION_REMOVED avoids bubbles vanishing abruptly mid-read just because the phone-side
conversation was opened/cleared.

### Consequence

This intentionally diverges from ARCHITECTURE.md 4.3's dedup/update model as originally written.

---

## ADR-009 — Pairing and transport encryption: TLS with trust-on-first-use pinning

### Decision

The Windows receiver upgrades from `ws://` to `wss://` using a self-signed certificate it
generates and persists on first run. Pairing is a short-lived (2 minute) 6-digit code shown on the
PC; the phone connects, HMAC-SHA256-signs the certificate's SHA-256 fingerprint with that code,
and sends it as proof in `PAIR_REQUEST`. The PC verifies the same computation against its own
fingerprint and, on match, generates a persistent per-device shared secret and returns it in
`PAIR_RESPONSE`. The phone pins that certificate fingerprint from then on (trust-on-first-use,
like an SSH host key) rather than trusting a certificate authority.

### Reason

Closes the "notification interception" and "unauthorized notification injection" threats in
SECURITY.md's threat model using only established platform primitives (TLS, HMAC-SHA256, SHA-256)
per SECURITY.md #7 -- no invented cryptography. Binding the pairing proof to the specific
certificate fingerprint defeats a simple man-in-the-middle: an attacker presenting a different
certificate can't forge a matching proof without knowing the code.

### Consequence

Port/discovery is manual for MVP: the user reads the PC's port (7787, fixed) and pairing code off
the Windows app and enters them on the phone. No automatic discovery (mDNS/broadcast) exists yet;
that remains a genuinely unresolved future decision, not blocking Phase 5's security goal.

---

## ADR-010 — Reconnect authentication: HMAC-SHA256 challenge-response

### Decision

Every connection (not just first pairing) must complete an `AUTHENTICATE` exchange before the PC
accepts any `NOTIFICATION`/`NOTIFICATION_REMOVED` message. The phone sends its deviceId, a fresh
nonce, a timestamp, and `HMAC-SHA256(sharedSecret, deviceId|nonce|timestamp)`; the PC recomputes
using its stored copy of that device's secret and rejects if it doesn't match, the timestamp is
missing/unparseable, or is older than 5 minutes.

### Reason

Satisfies SECURITY.md #2 ("the PC must reject notification data from untrusted peers") and #6
(authentication state as part of required input validation) on every connection, not just the
initial pairing handshake. Unpairing (removing a device from the PC's trusted-device store)
immediately revokes it, since future `AUTHENTICATE` attempts will fail the secret lookup.

---

## Network path findings (Phase 5 real-device testing)

Status legend: **Confirmed** (directly tested), **Likely** (strong evidence, not exhaustively
proven), **Not yet tested**.

- **Confirmed** -- home Wi-Fi (both devices on the same router, same subnet) **works end to end**:
  pairing, authentication, and live notification delivery all succeeded, with the Windows app
  listening on `0.0.0.0:7787` and its firewall rule allowing the Private profile that this network
  is classified as. No application or router change was needed to make this work.
- **Confirmed** -- using the Android phone's own Wi-Fi hotspot as the network (PC connects to the
  phone's hotspot, bypassing the home router entirely) also works end to end: pairing,
  authentication, and live notification delivery all succeeded.
- **Confirmed** -- Windows classifies a phone hotspot connection as network category "Public," not
  "Private." The app's firewall rule initially only allowed "Private" and had to be manually
  extended to include "Public" (`Set-NetFirewallRule -Profile Private,Public`) before the hotspot
  path worked. Home Wi-Fi is classified "Private" and worked under the original rule.
- **Revised, not AP isolation** -- earlier testing on this same home Wi-Fi failed with
  `ConnectException` on Android and `Destination Host Unreachable` on a raw ping test, which at the
  time looked like router-side AP/client isolation. Retesting later, after both devices had been
  reconnected to the network for longer, succeeded with no configuration changes on either device
  or the router. The most likely explanation is transient network state right after switching
  Wi-Fi networks (ARP cache / DHCP lease / Windows network-location detection not yet settled),
  not a persistent router restriction. AP isolation was never actually confirmed by inspecting the
  router itself, so this project does not conclude the router isolates clients -- if a similar
  failure recurs, retry after both devices have been connected for at least 30-60 seconds before
  concluding it's a router limitation.
- **Not yet tested** -- USB tethering as a network path (only `adb reverse`-tunneled loopback, home
  Wi-Fi, and phone Wi-Fi hotspot have been tested).
- Both home Wi-Fi and the phone hotspot are supported, working network paths. The phone hotspot
  remains useful as a fallback if home Wi-Fi is ever unreachable, but is not required.

---

## Unresolved decisions

The implementation team must explicitly decide:

- automatic endpoint discovery strategy (mDNS/broadcast); manual host:port entry is the accepted
  MVP behavior per ADR-009
- Android minimum SDK
- Windows minimum version
- icon transfer format
- notification update/removal semantics
- whether ongoing notifications should be forwarded
- behavior for silent notifications
- maximum bubble count
- click behavior
- whether hover-to-pause belongs in MVP
