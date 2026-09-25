# Phone Notification Bridge — Development Summary

**Project**: Mirrors Android notifications to a Windows desktop as temporary, non-intrusive bubbles, so the user doesn't have to pick up their phone to see what a notification says while working at a PC with an external monitor. Personal-use, local-first (no cloud), privacy-sensitive by design.

**Repo**: `github.com/OppiRah/small-notification-project` — `android/` (Kotlin, native Android app) and `windows/` (C#/.NET 9, WPF).

This document summarizes everything implemented, tested, broken, and fixed across Phases 1–7 of the project's roadmap, for handoff/discussion purposes.

---

## Architecture at a glance

```
Android phone                              Windows PC
──────────────                              ──────────
NotificationListenerService                 LocalWebSocketReceiver (TLS, wss://)
  → NotificationExtractor                      → ProtocolDecoder (validates envelope/payload)
  → NormalizedNotification                     → Pairing/Auth routing (in-connection)
  → ProtocolMessages (JSON envelope)            → NotificationManager (per-message bubbles,
  → TransportClient (TLS, pinned cert,             duration, max-visible cap)
     HMAC auth)                                → OverlayWindow (transparent, topmost,
  → PairingClient (one-shot pairing flow)          click-through, per-monitor/corner)
  → TrustedPcStore (persisted trust)            → Settings tab (monitor, corner, duration,
                                                    max bubbles, animation, start-with-Windows)
                                                → CertificateStore / TrustedDeviceStore /
                                                   PairingSession / AuthProof (Security/)
```

Protocol: JSON envelope over WebSocket-over-TLS (`wss://`), `{protocolVersion, messageType, messageId, timestamp, payload}`. Message types actively used: `NOTIFICATION`, `NOTIFICATION_REMOVED`, `PAIR_REQUEST`, `PAIR_RESPONSE`, `AUTHENTICATE`, `AUTH_RESULT`.

---

## Phase-by-phase summary

### Phase 1 — Android notification capture ✅
- `NotificationBridgeListenerService` (Android `NotificationListenerService`) captures posted/removed notifications.
- `NotificationExtractor` pulls title/body/bigText/expandedLines/summary/category generically (app-agnostic — no per-app special-casing, per ADR-003).
- Dev inspector (`MainActivity`) shows captured notifications on-device for verification.
- **Setback**: MIUI (Xiaomi/Redmi) silently refused to actually bind the listener service even after the user granted notification access and enabled autostart — `dumpsys activity services` showed nothing bound despite the permission being granted. **Fix**: a full phone reboot. This is a known MIUI quirk, not a bug in our code — documented as the standard remedy if this recurs.
- **Setback**: JDK not on PATH for Gradle builds. **Fix**: pointed `JAVA_HOME` at Android Studio's bundled JBR (`Program Files\Android\Android Studio\jbr`).

### Phase 2 — Windows receiver prototype ✅
- WPF app (`windows/src`), initial local WebSocket receiver via `HttpListener`.
- `ProtocolDecoder`: parses/validates the envelope and `NOTIFICATION` payload (size limits, required fields, type checks) — rejects malformed input cleanly rather than crashing.
- Dev panel with buttons to send synthetic valid/malformed test messages, so the receiver could be verified before Android was ever involved.
- Automated xUnit test suite added (`windows/tests`) covering the exact protocol-validation cases TESTING.md calls for.
- **Setback**: `System.Windows.Forms` + `System.Windows` namespace collision (`Application`, `UserControl` ambiguous) when briefly trying `Screen` for monitor info — resolved by switching to direct Win32 P/Invoke (`GetMonitorInfo`/`EnumDisplayMonitors`) instead, avoiding the WinForms dependency entirely.

### Phase 3 — End-to-end transport ✅
- Android `TransportClient` (OkHttp WebSocket, reconnect with bounded exponential backoff) + `ProtocolMessages.kt` (envelope/payload builders).
- Tested via `adb reverse tcp:7787 tcp:7787` (USB tunnel) — kept the receiver on loopback-only during this phase since no pairing/auth existed yet (a deliberate scoping decision to avoid an unauthenticated LAN-facing receiver).
- **Setback**: Android blocked all traffic with `CLEARTEXT communication to 127.0.0.1 not permitted` — Android 9+ blocks cleartext (`ws://`) by default. **Fix (temporary, later removed)**: a `network_security_config.xml` scoped to allow cleartext only to `127.0.0.1`. Removed entirely once Phase 5 made everything TLS.
- **Setback/discovery**: Android's own `Notification.CATEGORY_SERVICE` (Messenger's "Chat heads active") and `Notification.FLAG_GROUP_SUMMARY` (TikTok's group-summary placeholder) were flooding the bridge with noise. **Fix**: filter both out in the listener service — generic Android platform signals, not per-app special-casing, so still consistent with the app-agnostic design principle (ADR-003).

### Phase 4 — Bubble renderer ✅
- `OverlayWindow`: transparent, borderless, always-on-top, non-activating, click-through WPF window. Implemented via `TcpListener`/raw Win32 ex-styles (`WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, `WS_EX_TRANSPARENT`) rather than relying on WPF alone, since WPF has no native equivalent for "don't steal focus."
- Positioned using `SetWindowPos` in **physical pixels**, not WPF's device-independent units — necessary for correct placement regardless of a monitor's DPI scale factor.
- `BubbleControl`: rounded card, fade-in/fade-out animation, app name/title/body.
- `NotificationManager`: **deliberate product decision (ADR-008)** — every incoming message renders as its **own independent bubble** (keyed by the wire envelope's `messageId`), rather than the originally-planned "update the existing bubble in place" model. Reason: Android reuses the same `notificationId` for every new message within one conversation, so update-in-place would collapse a fast conversation down to "latest message only" and hide everything said in between. The user wants a running message log instead — newest bubble on top, each with an independent expiry timer, so multiple messages can be visible and stacked at once. `NOTIFICATION_REMOVED` is intentionally ignored for display (a bubble only ever disappears via its own timer, not because the phone-side conversation was read/cleared).
- Verified live: real back-to-back Messenger messages stack correctly, oldest expiring first.

### Phase 5 — Pairing/security ✅ (the big one)
This is the phase the project's own docs say must work before calling the system "production-ready."

**Design** (ADR-009, ADR-010):
- Windows generates and persists a **self-signed TLS certificate** on first run (`CertificateStore`, `.NET`'s built-in `CertificateRequest`/RSA — no hand-rolled crypto).
- Receiver upgraded from `ws://` to `wss://`.
- **Pairing**: PC shows a short-lived (2 min) 6-digit code + its IP. Phone connects, HMAC-SHA256-signs the certificate's SHA-256 fingerprint using that code as the key, and sends it as proof (`PAIR_REQUEST`). PC recomputes the same HMAC against its own fingerprint; if it matches, it proves the phone actually negotiated TLS with *this* PC (defeats a simple man-in-the-middle, since an attacker presenting a different certificate can't forge a matching proof without knowing the code). On success, PC generates a random persistent per-device secret and returns it (`PAIR_RESPONSE`).
- Phone pins the certificate's fingerprint from then on — **trust-on-first-use**, like an SSH host key, not a certificate authority.
- **Every subsequent connection** (not just pairing) must complete an `AUTHENTICATE` exchange: phone sends `deviceId`, a fresh nonce, a timestamp, and `HMAC-SHA256(sharedSecret, deviceId|nonce|timestamp)`. PC verifies against its stored secret and a 5-minute freshness window before accepting any `NOTIFICATION` traffic from that connection.
- **Unpair**: removing a device from the PC's trusted-device list immediately revokes it.

**New components**: Windows `Security/` namespace (`CertificateStore`, `PairingSession`, `TrustedDeviceStore`, `AuthProof`); Android `TlsTrust.kt` (trust-on-first-use + pinned `SSLSocketFactory`s), `TrustedPcStore.kt` (persisted paired-PC identity), `PairingClient.kt` (one-shot pairing flow).

**Setbacks encountered (this phase had several real ones):**

1. **`HttpListener` HTTPS requires admin-elevated `netsh http add sslcert` binding.** Unreasonable friction for a personal desktop app. **Fix**: rewrote the receiver on `TcpListener` + `SslStream` + a hand-rolled RFC 6455 WebSocket upgrade handshake (`Sec-WebSocket-Accept` via SHA-1 + the RFC's fixed magic GUID — that's the literal spec-mandated computation, not invented crypto), then handed the now-upgraded stream to .NET's built-in `WebSocket.CreateFromStream`. No admin rights needed.

2. **OkHttp's `Response.handshake` and `EventListener.secureConnectEnd` were unreliable for capturing the server's TLS certificate specifically on a `newWebSocket()` connection** — repeatedly returned `handshake != null` but `peerCertificates.size == 0`, or `handshake == null` entirely, even though the TLS handshake itself was completing fine. Tried two different OkHttp-native approaches before concluding this was a WebSocket-upgrade-specific quirk. **Fix**: bypass OkHttp's abstractions entirely for certificate capture — open a raw `SSLSocket` directly, call `.startHandshake()`, and read `.session.peerCertificates` off it. Completely reliable. The actual `PAIR_REQUEST`/`PAIR_RESPONSE` exchange still goes over a normal OkHttp WebSocket afterward.

3. **Windows Firewall**: an inbound rule for the app existed but only allowed the "Private" network profile. When testing over the phone's own hotspot, Windows classified that connection as "Public" — silently blocked until the rule was manually extended (`Set-NetFirewallRule -Profile Private,Public`, needs an elevated PowerShell).

4. **`GetLocalIPv4()` picked the wrong network adapter** — a naive "first non-loopback IPv4 from `Dns.GetHostAddresses`" approach grabbed a VirtualBox host-only adapter's IP (`192.168.56.1`) instead of the real WiFi IP. **Fix**: rewrote to prefer an adapter that actually has a **gateway configured** (i.e., a real network connection), falling back to any non-loopback adapter only if none do.

5. **The big scare**: home WiFi pairing failed with `ConnectException` on Android and `Destination Host Unreachable` on a raw ping test, even though both devices were confirmed on the same subnet and the Windows firewall/listener were confirmed correctly configured. This looked exactly like router-side AP/client isolation (common default on ISP-provided routers, blocks device-to-device LAN traffic while still allowing internet access). **Workaround used at the time**: switched to the Android phone's own WiFi hotspot as the network (PC connects to the phone's hotspot) — this worked immediately, pairing + auth + live notification delivery all succeeded.
   - **Later correction**: retested home WiFi again (without changing anything, router included) and it **worked perfectly** — full pairing, auth, and notification delivery succeeded. Since nothing about the router changed between the failure and the success, AP isolation was **never actually confirmed** (the router's own settings were never inspected) and is no longer the leading explanation. The most likely real cause: transient network state right after switching WiFi networks (ARP cache / DHCP lease / Windows network-location detection not yet settled) — the failed attempts were all made within seconds of switching networks. **Current documented conclusion**: both home WiFi and the phone hotspot are confirmed working, supported network paths; if a similar failure recurs, retry after ~30-60 seconds on the new network before concluding it's a router limitation. This nuance (Confirmed vs. Likely vs. Revised) is recorded explicitly in `DECISIONS.md`.

Verified end to end, twice, on real hardware: pairing → authentication → live notification delivery, over both home WiFi and phone hotspot.

### Phase 6 — Settings ✅
- Real Settings UI (previously everything lived in a "dev panel"): monitor picker (now enumerates **all** connected monitors via `EnumDisplayMonitors`, not just the primary), corner placement (all 4 options from UI_UX.md: top-left/top-right/bottom-left/bottom-right), bubble duration slider, max-visible-bubbles slider (oldest bubble evicted when over cap), animation on/off toggle, start-with-Windows checkbox (standard per-user `HKCU\...\Run` registry key, no installer/elevation needed).
- Settings persist to a JSON file (`%LOCALAPPDATA%\NotificationBridge\settings.json`) and apply **live** (no restart needed) via `OverlayWindow.ApplySettings()`.
- A "Developer" tab keeps the old synthetic-test tooling and raw message log for debugging.
- Verified live: changed corner to Bottom Left, applied, confirmed the overlay repositioned and a new test notification appeared in the right place.

### Phase 7 — Reliability ✅ (code complete; three manual scenarios still pending)
A code audit of the reconnect and receiver paths found real gaps, fixed in two commits (`b8c24db`, `b84cf7e`), each with tests written first.

**Connection health**
- **Half-open connections were never detected.** Neither side pinged, so after Wi-Fi dropped or the PC slept, the phone kept "sending" into a dead socket and notifications were silently lost. Fix: OkHttp ping interval (15s) on the phone; ping/pong with a 15s timeout (`WebSocketCreationOptions.KeepAliveTimeout`) on the PC.
- **Duplicate/stale connections.** `start()` is called from both `MainActivity` and the listener service, and auth-rejection could queue double reconnects. Fix: a generation counter so callbacks from a superseded socket are ignored, `start()` cancels any existing socket, and only one reconnect is ever pending.
- **Re-pair bug (found during on-device testing).** A reconnect timer queued for a previously paired PC held that PC's address; after re-pairing it fired, cancelled the good new connection, and retried the stale address forever. Fix: `start()` discards queued reconnects first. The generation-counter change had made this worse, which is how it surfaced.
- The PC's "client connected" status now counts live connections instead of flipping to "no client" when any one connection closes.

**Input hardening**
- **Message size caps** on the PC receiver (64 KB before authentication, 2 MB after); an oversized message closes the connection with `MessageTooBig`. Before, any LAN peer could stream unlimited data pre-auth.
- **Long notifications were being dropped entirely.** The PC rejects a whole notification that exceeds any protocol limit, and the phone sent full-length text. Fix: the phone clips app name (256), title (1024), body (8192), expanded lines (100 entries, 4096 chars each) with a trailing "…". Bubbles already cap themselves at 3 lines.
- Accept loop waits 250ms after an error instead of spinning; the Developer log is capped at 500 entries.

**Display/network changes (Windows)**
- Overlay re-places itself and the monitor list refreshes on display changes (unplug/rearrange); the status-text IP refreshes on network address changes.

**Verified on real hardware (home Wi-Fi):** PC app restart, phone Wi-Fi off/on (drop noticed in ~3s), PC app frozen then resumed (ping timeout detected in ~26s, reconnect ~3s after resume), PC-side detection of a vanished phone, delivery after recovery, a 10.8k-character notification. Windows tests: 27; Android unit tests: pass.

**Not yet tested (user will do):** PC sleep/wake, monitor unplug/replug, phone restart. The restart paths were only read through in code (pairing state and the TLS cert persist on both sides).

**Observed, not a bug:** reconnect after Wi-Fi returns took 34–67s in testing. The retry delay had reached its 30s cap during the outage, and the phone (weak signal, 1–3 bars) is slow to re-associate. The user chose not to add a network-change trigger (it would need a new `ACCESS_NETWORK_STATE` permission).

---

### Phase 8 — Polish ✅ (installer and signed release build included)
Built and verified on real hardware, in the order below. Windows tests: 33; Android unit tests: pass.

**Hover-to-pause** (`6c8e0bd`). The overlay is deliberately click-through (`WS_EX_TRANSPARENT`), so it receives no mouse events and normal hover handling can't work. Instead the overlay polls the cursor every 100ms against each bubble's on-screen rectangle (only while bubbles exist), which keeps it from ever intercepting a click. A hovered bubble's dismissal timer stops; on leave it restarts with the *full* duration. Verified with an 8s duration: still visible after 13s under the cursor, and gone 11s after leaving.

**App icons** (`cc76af8`, ADR-011). The phone renders the posting app's *launcher* icon (via `PackageManager`, app-agnostic per ADR-003) as a 96x96 PNG, caches it per package, and sends it base64-encoded in an optional `iconPng` field of every `NOTIFICATION` (`protocolVersion` stays 1). The PC accepts it only if it is ≤64 KB of base64, has a PNG signature, and declares ≤256x256 dimensions, so a malformed or decompression-bomb image never reaches WPF's decoder. **An unusable icon is ignored, never rejected**: an icon must not cost the user the notification (the lesson from the Phase 7 long-text bug). Decided with the user: launcher icon (not the monochrome small icon), sent with every notification.

**Visual polish** (`5170700`), all four options chosen by the user; dark theme only (following Windows light/dark was offered and declined):
- Faint border and drop shadow so cards stand out from dark windows; muted small app name, 14px semi-bold title, 12.5px body, Segoe UI Variable with Segoe UI fallback.
- Slide-in from the anchored screen edge with the fade, and existing bubbles ease into place (measure positions before/after a stack change, animate the difference) instead of jumping.
- Animations are skipped when either the in-app setting or Windows "Animation effects" is off; high-contrast mode uses system colors. **These two paths are untested** (Windows settings weren't safe to toggle from the test harness).
- Layout bug fixed along the way: a fixed 320-DIP bubble inside a 16px-margined 340px window clipped ~12px off every card's right edge and couldn't adapt to scaled monitors. Bubbles now stretch to the overlay width.

**Real-app bug found by the polish pass** (`5170700`). A real Messenger notification showed the raw package name `com.facebook.orca` and no icon; only a system-package test notification worked. Cause: Android 11+ hides other installed apps from an app by default, and notification-listener access doesn't change that, so *both* the pre-existing app-name lookup and the new icon lookup failed for every non-system app. Fix: a `<queries>` entry for launcher apps (`MAIN`/`LAUNCHER`) in the manifest, deliberately narrower than `QUERY_ALL_PACKAGES` (which Google Play restricts). Verified with real Messenger and Maya notifications. Recorded in ADR-011.

**Windows installer** (`d59e3c0`). Inno Setup (installed per-user via winget) with `windows/installer/build-installer.ps1`, which publishes self-contained (no .NET runtime needed on the target PC) and compiles `dist/NotificationBridge-Setup-0.1.0.exe` (~42 MB). Per-user install to `%LOCALAPPDATA%\Programs` (no admin prompt, matching the app's per-user autostart and data). Upgrades keep pairing data and settings; uninstall removes the program, shortcuts and the start-with-Windows entry. Verified: install, launch, phone reconnect with the existing pairing, bubble delivery, uninstall cleanup, reinstall.
- **Bug I introduced and fixed:** the uninstall script asked whether to delete pairing data via `MsgBox`, assuming a silent uninstall would answer "No". Inno Setup answers "Yes" in silent mode, so the first silent uninstall test wiped `%LOCALAPPDATA%\NotificationBridge` (TLS certificate, trusted devices, settings) and forced a re-pair. Fix: only prompt when `not UninstallSilent()`; a silent uninstall never deletes data. Re-tested: data survives byte-for-byte.

**Signed Android release build** (`d59e3c0`). Release signing reads `android/keystore.properties` (gitignored; without it the release build is simply unsigned). The key lives at `%USERPROFILE%\.notification-bridge\release.jks`, outside the repo; the user has backed up the password. The release APK (6 MB, `versionName` 0.1.0, launcher label no longer "(Dev)") verifies under APK Signature Scheme v2 and is not debuggable; R8 minification is off deliberately. Releases are signed with a different key than debug builds, so switching a phone means uninstalling the debug app (losing pairing and the notification-access grant); the user switched, re-granted access, and re-paired. Losing the key means future updates can't install over the old app.

**Repo housekeeping** (`0030b41`). Markdown docs moved into `docs/` (README stays at the root); `.gitignore` reorganized into labelled sections covering secrets/signing material (keystores, `*.pfx`/`*.p12`/`*.pem`/`*.key`, `.env*`, the paired-device store), machine-local IDE/tool state, and build output. Verified with `git check-ignore`; no sensitive file has ever been committed.

---

## Current status

**Phases 1–8 are working on real hardware**, apart from the untested items listed under "What's left" below. It is now installable: a self-contained Windows installer and a signed Android release APK. The system is usable day-to-day: pair once (persists across restarts), notifications from Messenger/Instagram/etc. mirror to Windows as bubbles, fully encrypted and authenticated, with real user-facing settings — over the user's actual home WiFi, recovering on its own from drops.

## What's left

- *(Phase 8 is complete — see its section above. Untested by the user so far: PC sleep/wake, monitor unplug/replug and phone restart from Phase 7, and reduce-motion/high-contrast rendering from Phase 8.)*
- **Phase 9 — Optional features** (each needs its own privacy/security review per the project's own rules): per-app filters, notification history, click actions, dismiss-from-PC, reply actions, grouping, sounds, custom themes.

## Known limitations

- Endpoint discovery is manual (user reads the PC's IP+port off the Windows app and types it into the phone) — no mDNS/auto-discovery yet. Explicitly deferred, not a bug. Consequence: the phone stores the PC's address at pairing time, so if the PC changes network (e.g. from the phone's hotspot to home Wi-Fi) or its DHCP address changes, the phone keeps retrying the old address until re-paired.
- Notifications that arrive while the phone is disconnected are dropped, not queued (deliberate: replaying stale messages minutes later would be confusing).
- Only one Windows PC can be paired with a given phone at a time in the current UI flow (the architecture supports multiple trusted devices on the PC side, but there's no multi-PC pairing UI on the phone).
- The Windows installer is not code-signed, so Windows SmartScreen may show an "unknown publisher" warning when it is run. The Android release is signed with a self-generated key (not distributed through the Play Store).
- Only Narrator-visible basics exist for accessibility: there is no screen-reader announcement of new bubbles, and the app has no custom executable icon.
