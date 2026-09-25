# Phone Notification Bridge

## Project purpose

A personal-use Android → Windows notification bridge.

The phone is the **only source of notifications**. When Android receives a notification, the companion Windows application receives the notification over a local connection and renders it as a temporary, rich desktop bubble.

The core user experience is:

> **Phone notification → local connection → PC → unobtrusive desktop bubble → automatic dismissal**

The project exists to remove the repeated need to look away from the monitor, pick up the phone, unlock it, and expand a notification merely to see what it says.

This is intentionally **not** a phone-control platform, AI assistant, cloud service, or general remote-control system.

---

## Getting started

Everything stays on your local network. You need a Windows PC and an Android phone (Android 10+) on the same Wi-Fi.

### 1. Install the Windows app

Run `NotificationBridge-Setup-<version>.exe`. It installs for your user only (no administrator prompt) and includes the .NET runtime it needs. To build the installer yourself, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run:

```powershell
powershell -File windows\installer\build-installer.ps1    # writes dist\NotificationBridge-Setup-<version>.exe
```

- The installer is not code-signed, so Windows SmartScreen may warn about an unknown publisher.
- On first launch Windows Firewall asks whether to allow the app. Allow it on **Private** networks (home Wi-Fi). A phone hotspot is classed as **Public**, so to use one you must also allow the app on Public networks (Windows Security → Firewall → Allow an app).
- Upgrades keep your pairing and settings. Uninstalling asks whether to delete them too.

### 2. Install the Android app

Build a release APK (see below) and install it: `adb install android\app\build\outputs\apk\release\app-release.apk`.

Then turn on notification access for **Notification Bridge**: tap **Open notification access settings** in the app (or go to Android Settings → Notification access). On Xiaomi/MIUI phones also allow autostart; if notifications still don't arrive, restart the phone once (MIUI sometimes fails to bind the listener until a reboot).

### 3. Pair the phone with the PC

1. On the PC, open the app's **Settings** tab and click **Pair new device**. It shows a 6-digit code (valid for 2 minutes) and the PC's address, like `192.168.1.7:7787`.
2. In the phone app, enter the **PC IP address**, **PC port** (`7787`) and **Pairing code**, then tap **Pair**.

Pairing happens once and survives restarts. Remove a phone with **Unpair selected** on the PC. There is no automatic discovery, and the phone remembers the PC's address, so if the PC's IP address changes (or it moves to a different network) pair again.

### Build from source

```powershell
# Windows
dotnet run --project windows/src
dotnet test windows/tests

# Android (set JAVA_HOME to a JDK 17+, e.g. Android Studio's bundled JBR)
cd android
.\gradlew.bat :app:installDebug          # debug build onto a connected phone
.\gradlew.bat :app:testDebugUnitTest
.\gradlew.bat :app:assembleRelease       # release APK (signed if the keystore below exists)
```

**Signing a release build.** Create a keystore with `keytool`, keep it **outside** the repository, and put its details in `android/keystore.properties` (gitignored):

```properties
storeFile=C:/path/to/release.jks
storePassword=...
keyAlias=...
keyPassword=...
```

Back up the keystore and its password: if you lose them, updated releases can't be installed over an existing install. Debug and release builds are signed with different keys, so moving a phone from one to the other means uninstalling first (you will need to grant notification access and pair again).

### Documentation

Design and planning documents live in [`docs/`](docs):

| File | What it covers |
|---|---|
| `PRODUCT.md`, `REQUIREMENTS.md` | What the app should do and the settings it exposes |
| `ARCHITECTURE.md`, `PROTOCOL.md` | Components, and the wire protocol between phone and PC |
| `SECURITY.md` | Threat model, pairing and authentication rules |
| `UI_UX.md` | Bubble and monitor behavior |
| `DECISIONS.md` | Architecture decision records (ADRs) and unresolved questions |
| `ROADMAP.md`, `DEVELOPMENT.md`, `TESTING.md` | Phases, engineering guidance, test plan |
| `SESSION_SUMMARY.md` | What has been built, tested, broken and fixed so far |

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
