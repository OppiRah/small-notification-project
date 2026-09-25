# Security and Privacy Requirements

## Threat model

This is a personal utility, but the data it handles can be highly sensitive.

Threats include:

- another device on the LAN connecting to the PC
- unauthorized notification injection
- notification interception
- accidental notification logging
- stale trusted devices
- malicious/malformed protocol messages
- compromised local applications attempting to connect
- accidental cloud transmission
- exposed listening ports

---

## Requirements

### 1. No cloud dependency

Notification contents must travel:

```text
Phone → local network → user's PC
```

not:

```text
Phone → third-party server → PC
```

---

### 2. Authentication

The PC must reject notification data from untrusted peers.

Pairing should create a persistent trust relationship.

Unpairing must revoke that relationship.

---

### 3. Least exposure

Prefer binding the listener to the required local interface/address.

Do not expose the service publicly.

Document the selected port and Windows firewall behavior.

---

### 4. No notification content in normal logs

Bad:

```text
INFO Received notification:
"John: Here is the private message..."
```

Prefer:

```text
INFO Received notification from package=com.example
```

Debug logging can be explicitly enabled for development, but should make the privacy implications obvious.

---

### 5. No unnecessary persistence

Notification bodies should exist in memory only for the duration necessary to process/render them.

MVP should not maintain a notification database.

---

### 6. Input validation

All network data is untrusted.

Validate:

- message type
- protocol version
- size
- strings
- arrays
- identifiers
- timestamps
- authentication state

---

### 7. Cryptography

Do not implement cryptographic primitives manually.

Use platform/library primitives.

The final design should document:

- key generation
- key storage
- pairing
- authentication
- encryption
- key rotation/revocation

If transport encryption is provided by the selected protocol, document exactly what is protected.

---

## Implementation status (v0.1.0)

How the requirements above are met by the shipped code. Pairing and authentication are specified in
DECISIONS.md (ADR-009, ADR-010).

| Requirement | How it is met |
|---|---|
| 1. No cloud | Phone connects directly to the PC over the LAN. No server, account or analytics exist. |
| 2. Authentication | Every connection must complete `AUTHENTICATE` (HMAC-SHA256 with a per-device secret, 5-minute timestamp window) before any notification is accepted. Unpairing on the PC revokes the device immediately. |
| 3. Least exposure | The receiver listens on all interfaces on port 7787 (the phone must be able to reach it); nothing is accepted without pairing/authentication. Windows Firewall asks on first launch; allow Private networks only (Public only if you use a phone hotspot, see README). |
| 4. No content in logs | Android logs metadata only (package, category, key). The Windows Developer tab logs message type and package, never title or body. Neither logs secrets. |
| 5. No persistence | Notification text lives in memory only. Persisted: pairing data and settings. |
| 6. Input validation | Every message is validated by `ProtocolDecoder` (version, type, sizes, arrays, timestamps, auth state) and size-capped (64 KB pre-auth, 2 MB after). Icons must be a small, PNG-signed image with sane dimensions. |
| 7. Cryptography | Only platform primitives: TLS 1.2/1.3, HMAC-SHA256, SHA-256, `RandomNumberGenerator` / `SecureRandom`. Comparisons are constant-time. |

### Known limitations (accepted for a personal LAN tool)

- **Pairing code strength.** The 6-digit code is single-use and dies after 5 wrong attempts, which
  stops guessing. An attacker who is actively in the middle *during* the 2-minute pairing window can
  still recover the code offline (see DECISIONS.md, ADR-009). Pair on a network you trust.
- **Secrets are not encrypted at rest.** The PC's TLS private key and paired-device secrets sit in
  `%LOCALAPPDATA%\NotificationBridge` (protected by your Windows user account); the phone's copy is in
  the app's private storage (`allowBackup` is off). Malware running as you could read them.
- **No replay cache for `AUTHENTICATE`.** Nonces are not remembered. This is acceptable because the
  proof only ever travels inside the pinned TLS channel, and it is bounded by the 5-minute window.
- **Unauthenticated connections are not time-limited.** A device on your LAN can open a connection
  and hold it idle (it can never deliver a notification). This is a nuisance-level denial of service
  only; the "client connected" indicator also counts such connections.
- **Clock skew.** If the phone's and PC's clocks differ by more than 5 minutes, authentication fails.
  Leave both on automatic time.
- **Distribution.** The Windows installer is unsigned (SmartScreen warns) and the APK is signed with
  a self-generated key, not distributed via the Play Store.

---

## Privacy boundary

The application may receive:

- personal messages
- email previews
- authentication/security alerts
- financial notifications
- private social-media content
- two-factor authentication notifications
- system notifications

Therefore, notification contents must be treated as sensitive data by default.
