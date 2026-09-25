# Development Roadmap

## Current phase

```text
Current: v0.1.0 release hardening -- Phases 1-8 are complete and working on real hardware (Phase 8
delivered hover-to-pause, app icons, visual polish, the Windows installer and a signed Android
release APK). The final audit pass tightened pairing (single-use, attempt-limited code) and removed
secrets/notification text from logs; see SESSION_SUMMARY.md. Still unverified on hardware: PC
sleep/wake, monitor unplug/replug, phone restart, and reduce-motion / high-contrast rendering (a
checklist is in TESTING.md). Phase 9 is optional and not started; do not begin it unless a real need
appears.)
```

Update this line as work progresses. Any session (you or Claude Code) should check here first
to know what's active before picking up work.

---

## Phase 0 — Research and decisions

Before coding:

- confirm Android notification-listener behavior on target Android version
- determine notification extraction capabilities
- choose Windows UI framework
- choose transport
- define protocol
- design pairing/authentication
- identify Android background/service limitations
- identify Windows firewall requirements
- define MVP acceptance tests

Deliverable:

```text
Architecture decision record
```

---

## Phase 1 — Android notification prototype

Goal:

Receive Android notifications and inspect their available data.

Build:

- minimal Android application
- notification listener service
- developer-only notification inspector
- structured logging

Test against:

- messaging app
- social media
- email
- browser
- system notification
- media notification
- grouped notifications
- ongoing notification
- notification with expanded content
- notification with multiple messages

Do not build the Windows UI yet.

---

## Phase 2 — Windows receiver prototype

Goal:

Receive a synthetic notification from a test client.

Build:

- Windows application
- local receiver
- protocol parser
- validation
- connection status

Use synthetic test messages before connecting Android.

---

## Phase 3 — End-to-end transport

Goal:

```text
Android notification
        ↓
Windows receiver
```

No polished UI yet.

Display received notifications in a developer/test panel.

Acceptance:

- notification arrives
- fields match
- malformed messages are rejected
- reconnect works
- duplicate behavior is understood

---

## Phase 4 — Bubble renderer

Build:

- transparent overlay
- topmost behavior
- monitor selection
- corner placement
- bubble component
- fade animation
- timer
- stacking

Acceptance:

- overlay does not steal focus
- target monitor works
- multiple bubbles work
- bubbles expire independently

---

## Phase 5 — Pairing/security

Build:

- pairing UI
- trusted device identity
- authentication
- unpair
- reconnect
- invalid-device rejection

Do not call the system production-ready until this phase works.

---

## Phase 6 — Settings

Add:

- monitor selector
- position selector
- duration
- max visible notifications
- animation
- startup behavior
- connection status
- pairing management

---

## Phase 7 — Reliability

Test:

- Wi-Fi disconnect
- Wi-Fi reconnect
- phone sleep
- PC sleep
- PC wake
- app restart
- phone restart
- Windows restart
- monitor unplug/replug
- monitor rearrangement
- notification bursts
- very long notifications
- malformed protocol messages

---

## Phase 8 — Polish

Only after reliability:

- icon extraction
- typography
- theme
- better animations
- hover pause
- accessibility
- installer
- documentation

---

## Phase 9 — Optional features

Only if the core product remains stable:

- per-app filters
- notification history
- click actions
- dismiss-from-PC
- reply actions
- grouping
- sounds
- custom themes

Each feature requires a separate privacy/security review.
