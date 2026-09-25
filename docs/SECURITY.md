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
