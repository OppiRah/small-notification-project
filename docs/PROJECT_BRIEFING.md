# Phone Notification Bridge — Full Project Briefing

A single, self-contained handoff of the whole project, from the original idea through the latest
work (Phase 8, dated 2026-09-25). It condenses the 11 design docs plus the full build history, so
you don't need any other file to get up to speed.

---

## 1. What it is and why

**The problem.** The user works on a laptop with an external monitor. Every time the phone buzzes
they have to look away, pick it up, unlock it, and often expand the notification just to read it.

**The product.** *Phone Notification Bridge* mirrors Android notifications to a Windows desktop as
temporary, non-intrusive bubbles that stack, expire on their own timers, and never steal focus.

```
Phone notification → local connection → PC → unobtrusive desktop bubble → automatic dismissal
```

**Character of the project:** personal use, local-first (no cloud, no accounts, no analytics),
privacy-sensitive by design (notification text can include private messages, email previews, 2FA
codes), and built as a learning project that is still engineered like a small real utility.

**Explicit non-goals for the MVP:** replying to messages, controlling phone apps, SMS sending,
screen mirroring, remote control, AI summarization, cloud sync, account system, multi-user
infrastructure, cross-platform support, generating notifications from the PC.

**Engineering priorities, in order:** correctness → reliability → privacy/security → simplicity →
maintainability → testability → usability → visual polish. No architecture added just to look
sophisticated.

**Core design principle:** the system is built around the *notification abstraction*, not
individual apps. There is no `if Messenger…` anywhere (ADR-003). Android produces a normalized
notification object, and an unknown app must work without changing the Windows side. Android owns
notification truth; the PC never polls or reconstructs.

---

## 2. Tech stack and repo

| Part | Stack |
|---|---|
| Android app (`android/`) | Kotlin, native, `NotificationListenerService`, OkHttp WebSocket, Android 10+ |
| Windows app (`windows/src`) | C# / .NET 9, WPF |
| Tests | `windows/tests` (xUnit, 37 tests); Android unit tests (Robolectric, 18 tests) |
| Installer | Inno Setup, `windows/installer/build-installer.ps1` |
| Docs | `docs/` (11 md files) + root `README.md` |

Repo: `github.com/OppiRah/small-notification-project`. Branch `master`; working tree clean as of the
last commit.

---

## 3. Architecture

```
Android phone                              Windows PC
──────────────                              ──────────
NotificationListenerService                 Receiver (TcpListener + SslStream, wss://)
  → NotificationExtractor                      → ProtocolDecoder (validates envelope/payload)
  → NormalizedNotification                     → Pairing/Auth routing (per connection)
  → ProtocolMessages (JSON envelope)            → NotificationManager (per-message bubbles,
  → TransportClient (TLS, pinned cert,             duration, max-visible cap, timers)
     HMAC auth, reconnect w/ backoff)          → OverlayWindow (transparent, topmost,
  → PairingClient (one-shot pairing)              click-through, per-monitor/corner)
  → TrustedPcStore (persisted trust)           → Settings tab (monitor, corner, duration,
  → AppIconProvider (launcher icons)              max bubbles, animation, start-with-Windows)
                                               → Security/: CertificateStore, TrustedDeviceStore,
                                                  PairingSession, AuthProof
```

Architecture rules the project holds itself to: UI never parses network packets; the network layer
never renders; the Android listener doesn't own networking; extraction is independent of transport;
all network input is untrusted; timers have explicit owners; logging respects privacy.

---

## 4. Protocol

JSON envelope over WebSocket-over-TLS: `{protocolVersion, messageType, messageId, timestamp, payload}`.
`protocolVersion` is still `1`. Message types in use: `NOTIFICATION`, `NOTIFICATION_REMOVED`,
`PAIR_REQUEST`, `PAIR_RESPONSE`, `AUTHENTICATE`, `AUTH_RESULT`.

`NOTIFICATION` payload: `notificationId`, `packageName`, `appName`, `title`, `body`,
`expandedLines[]`, `summary`, `timestamp`, `category`, and optional `iconPng` (base64 PNG).

**Limits enforced by the PC (whole notification is rejected if exceeded):** app name 256, title
1024, body 8192, expanded lines 100 entries × 4096 chars. The phone clips text to these limits with a
trailing "…" before sending. Message-size caps on the socket: 64 KB before authentication, 2 MB
after (oversize closes the connection). Delivery is at-most-once; notifications that arrive while
disconnected are dropped, not queued.

---

## 5. Security design

- **Transport:** Windows generates a persistent self-signed TLS cert on first run (built-in .NET
  APIs). Receiver is `wss://` on fixed port **7787**. No hand-rolled crypto anywhere; only TLS,
  HMAC-SHA256, SHA-256.
- **Pairing (ADR-009):** PC shows a 6-digit code valid 2 minutes plus its IP. The phone opens TLS,
  captures the cert's SHA-256 fingerprint, and sends `HMAC-SHA256(code, fingerprint)` as proof. The
  PC recomputes against its own fingerprint; a match proves the phone really negotiated TLS with this
  PC (a man-in-the-middle presenting another cert can't forge it without the code). The PC then
  issues a random per-device secret. The phone pins the fingerprint from then on
  (trust-on-first-use, like an SSH host key). The code is single-use and is discarded after 5 wrong
  proofs. Accepted limit: an attacker actively in the middle *during* the 2-minute pairing window
  can recover the 6-digit code offline, so pair on a trusted network.
- **Every connection (ADR-010):** phone sends `deviceId`, fresh nonce, timestamp, and
  `HMAC-SHA256(secret, deviceId|nonce|timestamp)`. PC verifies against its stored secret with a
  5-minute freshness window before accepting any notification traffic.
- **Unpair** on the PC revokes immediately (future auth fails the secret lookup).
- **Also accepted for v0.1.0:** secrets are not encrypted at rest (protected by the OS user profile /
  app-private storage), there is no `AUTHENTICATE` replay cache (the proof only travels inside the
  pinned TLS channel), idle unauthenticated connections are not time-limited, and phone/PC clocks must
  agree within 5 minutes.
- **Privacy defaults:** notification bodies are never persisted and never written to normal logs; no
  history database in the MVP (ADR-005); nothing leaves the phone except to the paired PC.
- **Untrusted input:** all network data is validated (type, version, sizes, arrays, timestamps, auth
  state) before it can reach the UI. Icons are accepted only if ≤64 KB base64, valid PNG signature,
  ≤256×256 declared size (decompression-bomb guard); a bad icon is ignored but never costs the user
  the notification.

---

## 6. Key product decisions (ADRs, condensed)

| # | Decision | Why |
|---|---|---|
| 001 | Local-first; no cloud | Sensitive data; cloud adds cost, latency, exposure |
| 002 | `NotificationListenerService` | The platform API meant for this |
| 003 | App-agnostic, no per-app code | Goal is "show whatever Android reports" |
| 004 | Custom overlay, not Windows toast | Need exact monitor, position, layout, lifetime, stacking |
| 005 | No notification database | Ephemeral bridge; persistence is privacy risk |
| 006 | WPF (.NET) | Most direct path to transparent click-through topmost overlay; plain exe, no MSIX |
| 007 | WebSocket over LAN | Persistent, bidirectional, simple framing, no polling |
| 008 | **One bubble per message**, not update-in-place; `NOTIFICATION_REMOVED` ignored for display | Android reuses one notificationId per conversation, so update-in-place would hide everything said in a fast chat. User wants a running log; each bubble expires independently |
| 009/010 | TLS + TOFU pinning, HMAC pairing and per-connection auth | See §5 |
| 011 | Launcher icon (not monochrome small icon) sent as base64 PNG with every notification; `<queries>` for launcher apps instead of `QUERY_ALL_PACKAGES` | Recognisable logo; app-agnostic; narrower than a Play-restricted permission |

---

## 7. Build history: phase by phase

Each phase was verified on real hardware (a Xiaomi/Redmi phone on MIUI, a Windows 11 laptop).

**Phase 1 — Android capture ✅.** Listener service, generic extractor, on-device dev inspector.
*Setbacks:* MIUI silently refused to bind the listener even with access granted (fix: reboot the
phone; known MIUI quirk). Gradle couldn't find a JDK (fix: point `JAVA_HOME` at Android Studio's
bundled JBR).

**Phase 2 — Windows receiver ✅.** WPF app, `ProtocolDecoder`, dev panel that fires synthetic
valid/malformed messages so the PC side was verified before Android was involved, xUnit tests.
*Setback:* WinForms/WPF namespace collisions when trying `Screen` for monitors, so switched to Win32
P/Invoke (`EnumDisplayMonitors`/`GetMonitorInfo`).

**Phase 3 — End-to-end transport ✅.** OkHttp WebSocket client with bounded exponential backoff;
tested over an `adb reverse` USB tunnel with the receiver kept loopback-only (no auth existed yet, so
deliberately no LAN exposure). *Setbacks:* Android 9+ blocks cleartext (temporary scoped
network-security-config, removed once TLS landed). Android's own `CATEGORY_SERVICE` (Messenger "chat
heads active") and `FLAG_GROUP_SUMMARY` (TikTok placeholders) flooded the bridge; filtered both as
generic platform signals, still consistent with ADR-003.

**Phase 4 — Bubble renderer ✅.** Transparent, borderless, topmost, non-activating, click-through
overlay using raw Win32 extended styles, positioned in physical pixels for DPI correctness. Per-message
bubbles (ADR-008). Verified with real back-to-back Messenger messages.

**Phase 5 — Pairing and security ✅ (the big one).** Full design in §5. *Setbacks, all solved:*
(1) `HttpListener` HTTPS needs an admin `netsh` binding, so the receiver was rewritten on
`TcpListener` + `SslStream` + a hand-rolled RFC 6455 upgrade (the spec's fixed SHA-1 + magic GUID
computation), then handed to .NET's `WebSocket.CreateFromStream`, so no admin needed.
(2) OkHttp's handshake/`EventListener` unreliably exposed the server cert on a WebSocket upgrade; fix
was a raw `SSLSocket`, `startHandshake()`, and read `peerCertificates`. (3) Windows Firewall rule
covered only the Private profile; a phone hotspot is classed Public, so it was silently blocked until
extended. (4) `GetLocalIPv4()` picked a VirtualBox adapter (`192.168.56.1`); now prefers an adapter
with a gateway. (5) Home Wi-Fi pairing once failed and looked like router AP isolation; a hotspot
worked around it. Retesting later worked with nothing changed, so AP isolation was **never
confirmed** and the likely cause is transient network state right after switching networks. Rule of
thumb now: retry after 30–60 s on a new network before blaming the router. Both home Wi-Fi and
hotspot are confirmed working.

**Phase 6 — Settings ✅.** Real Settings tab: monitor picker (all monitors), four corners, duration
slider, max-visible slider (oldest evicted), animation toggle, start-with-Windows (per-user `HKCU`
Run key). Persisted to `%LOCALAPPDATA%\NotificationBridge\settings.json` and applied live. Old test
tooling lives in a "Developer" tab.

**Phase 7 — Reliability ✅ (code complete).** Audit found real gaps:
- Half-open connections were never detected, so notifications were silently lost after Wi-Fi drop or
  PC sleep. Fixed with ping/pong keepalive both sides (15 s).
- Duplicate/stale connections fixed with a generation counter and single pending reconnect. A
  re-pair bug (stale reconnect timer holding the old PC address) surfaced during device testing and
  was fixed by discarding queued reconnects on `start()`.
- Input hardening: size caps (64 KB pre-auth / 2 MB post-auth). Long notifications were being
  dropped entirely because the phone sent over-limit text, so the phone now clips.
- Overlay re-places itself on display changes; IP text refreshes on network changes.
- Verified on hardware: PC app restart, Wi-Fi off/on (~3 s to notice), PC app frozen/resumed (~26 s to
  detect), vanished-phone detection, a 10.8k-char notification. Reconnect after Wi-Fi returns took
  34–67 s (backoff hit its 30 s cap during the outage plus slow phone re-association); the user chose
  not to add a network-change trigger (it would need `ACCESS_NETWORK_STATE`).

**Phase 8 — Polish ✅ (installer and signed release included).**
- **Hover-to-pause.** The overlay is click-through so it gets no mouse events; instead it polls the
  cursor every 100 ms against bubble rectangles. Hovered bubbles pause; on leave the full duration
  restarts. Verified live.
- **App icons (ADR-011).** Launcher icon, 96×96 PNG, cached per package, sent in `iconPng`.
- **Visual polish** (all four options picked by the user; dark theme only, following Windows
  light/dark was offered and declined): border and shadow, typography, slide-in plus eased restacking,
  animations skipped when the in-app setting or Windows "Animation effects" is off, high-contrast
  support. A layout bug that clipped ~12 px off each card's right edge was fixed along the way.
- **Real-app bug found by the polish pass:** real Messenger notifications showed the raw package name
  `com.facebook.orca` and no icon, because Android 11+ hides other installed apps from an app by
  default. Fixed with a narrow `<queries>` launcher entry in the manifest.
- **Windows installer.** Inno Setup, self-contained (no .NET needed), per-user install to
  `%LOCALAPPDATA%\Programs`, ~42 MB `NotificationBridge-Setup-0.1.0.exe`. Upgrades keep pairing data
  and settings. *A bug the assistant introduced and fixed:* the uninstaller's "delete pairing data?"
  `MsgBox` auto-answers Yes in silent mode, so a silent uninstall wiped the TLS cert, trusted
  devices and settings; now it only prompts when not silent, and silent uninstalls never delete data.
- **Signed Android release.** Signing reads gitignored `android/keystore.properties`; key lives
  outside the repo at `%USERPROFILE%\.notification-bridge\release.jks` (password backed up by the
  user). 6 MB APK, v0.1.0, APK Signature Scheme v2, not debuggable, R8 off deliberately. Debug and
  release use different keys, so switching means uninstalling (re-grant access, re-pair).
- **Repo housekeeping.** Docs moved into `docs/`; `.gitignore` hardened for secrets and signing
  material; verified no sensitive file was ever committed. README now has a Getting-started guide.

**v0.1.0 release audit (final hardening pass, no new features).** Read the actual source and fixed
what was really wrong: the phone logged the pairing response containing the shared secret (now
logs nothing from it, and a malformed reply is a failure rather than an exception); the pairing code
had no attempt limit (now single-use, dead after 5 wrong proofs); the Windows Developer log kept
notification titles and bodies (now type and package only); and the Developer "send valid test
notification" button no longer worked since Phase 5 (now shows a bubble locally). Docs were
reconciled with the code. Windows tests 37, Android tests 18, all passing; installer and signed APK
rebuilt.

---

## 8. Current status

**Phases 1–8 are working on real hardware.** The system is installable (Windows installer + signed
APK) and usable day to day: pair once (persists across restarts), notifications from Messenger,
Instagram and others mirror as bubbles, fully encrypted and authenticated, with user-facing
settings, over the user's real home Wi-Fi, recovering from drops on its own.

**Still untested (user to do):** PC sleep/wake, monitor unplug/replug, phone restart (Phase 7); and
reduce-motion / high-contrast rendering (Phase 8). Those code paths were only read through, not run;
a step-by-step checklist is in `TESTING.md`. A clean install on a second machine was not done either.

## 9. Known limitations

- **No auto-discovery.** The user types the PC's IP and port into the phone. The phone stores that
  address at pairing time, so if the PC changes network or DHCP address the phone keeps retrying
  the old one until re-paired. Explicitly deferred (mDNS is the natural next step), not a bug.
- Notifications during a disconnect are dropped, not queued (replaying stale messages would confuse).
- One PC per phone in the UI (architecture supports multiple trusted devices on the PC side).
- Windows installer isn't code-signed (SmartScreen "unknown publisher" warning). Android APK is
  self-signed and not on the Play Store.
- Accessibility is basic: no screen-reader announcement of new bubbles; the app has no custom
  executable icon.
- USB tethering as a network path is untested.

## 10. What's next

**Phase 9 — optional features**, each needing its own privacy/security review: per-app filters,
notification history, click actions, dismiss-from-PC, reply actions, grouping, sounds, custom themes.
The roadmap says to touch these only if the core stays stable. Other open items: auto-discovery,
code-signing the installer, the untested scenarios above, and open questions still listed in
`DECISIONS.md` (Android min SDK, Windows min version, forwarding ongoing/silent notifications,
click behavior).

---

## 11. Notes on the docs themselves

The source docs were reconciled with the code during the v0.1.0 audit (`ROADMAP.md` current phase,
`PROTOCOL.md` no longer "Draft" and now lists the message types actually used, `DECISIONS.md` open
questions resolved, `SECURITY.md` implementation status and limitations). Two spots keep their
original text on purpose and carry an "as built" note: `ARCHITECTURE.md` §4.3/§10 describes
update-in-place, which ADR-008 overrides, and its message-type list includes types that were never
needed (`HELLO`, `HEARTBEAT`, `ACK`, `ERROR`).

Also worth knowing: closing the Windows app's window stops the bridge (no tray icon), and "Start with
Windows" opens that window at sign-in.
