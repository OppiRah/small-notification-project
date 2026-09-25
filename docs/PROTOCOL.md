# Protocol Specification

## Status

Version 1, implemented (`protocolVersion` is `1`). Transport is WebSocket over TLS (`wss://`, port
7787). The examples below match what the phone and PC actually send; section 10 lists the message
types in use. Anything not listed there (`HELLO`, `HEARTBEAT`, `ACK`, `ERROR`) is recognized by the
PC's decoder but never sent: liveness is handled by WebSocket ping/pong instead.

---

## 1. Goals

The protocol must be:

- local-first
- versioned
- explicit
- debuggable
- authenticated
- resilient to malformed input
- independent of UI technology
- capable of future backward-compatible extension

---

## 2. Transport

Candidate:

**WebSocket over local network**

Reasons:

- persistent bidirectional connection
- natural event delivery
- simple framing
- easy heartbeat/reconnect model
- no polling

Alternative:

**TCP with application-level framing**

Use only if the implementation has a clear reason to avoid WebSocket.

Do not use periodic HTTP polling for notification delivery.

---

## 3. Envelope

Conceptual:

```json
{
  "protocolVersion": 1,
  "messageType": "notification",
  "messageId": "uuid",
  "timestamp": "ISO-8601",
  "payload": {}
}
```

### Required properties

- protocol version
- message type
- unique message ID
- timestamp
- payload appropriate to message type

---

## 4. Notification payload

Conceptual:

```json
{
  "notificationId": "android-notification-key",
  "packageName": "com.example",
  "appName": "Example",
  "title": "Sender",
  "body": "Message body",
  "expandedLines": [
    "Message 1",
    "Message 2"
  ],
  "summary": null,
  "timestamp": "2026-09-20T10:00:00Z",
  "category": "message",
  "iconPng": "<base64 PNG, optional>"
}
```

Optional fields should be omitted or explicitly null according to the final schema convention.

`iconPng` is the posting app's launcher icon, rendered by the phone as a 96x96 PNG and sent with
every notification (ADR-011). It is optional and additive, so `protocolVersion` stays 1. The PC
accepts it only if it is at most 64 KB of base64, decodes to a PNG signature, and declares
dimensions no larger than 256x256. An icon that fails any of these is ignored; the notification
itself is still shown.

---

## 5. Validation

The receiver must validate:

- protocol version
- message type
- required fields
- field lengths
- array lengths
- UTF-8 validity
- timestamp format
- authentication state

Never deserialize arbitrary input directly into a trusted UI object.

---

## 6. Size limits

Hard maximums, enforced by the PC (`ProtocolLimits`); a notification that exceeds any of them is
rejected whole:

```text
app name:       256 chars
title:          1024 chars
body:           8192 chars
expanded lines: 100 entries
line length:    4096 chars
icon:           64 KB of base64, PNG, at most 256x256 (an unusable icon is ignored, not rejected)
message:        64 KB before authentication, 2 MB after
```

The phone clips text to these limits (with a trailing "…") before sending, so long notifications
still arrive.

---

## 7. Errors

Protocol errors should be machine-readable.

Conceptual:

```json
{
  "protocolVersion": 1,
  "messageType": "error",
  "messageId": "uuid",
  "payload": {
    "code": "INVALID_MESSAGE",
    "message": "Notification payload failed validation"
  }
}
```

Do not send stack traces to the remote peer.

---

## 8. Versioning

Rules:

- never silently change the meaning of an existing field
- add optional fields for backward-compatible extensions
- increment major protocol version for breaking changes
- reject unsupported major versions cleanly
- document compatibility

---

## 9. Authentication

Notification messages must not be accepted before authentication.

Conceptual state:

```text
CONNECTED
    ↓
UNAUTHENTICATED
    ↓
AUTHENTICATING
    ↓
AUTHENTICATED
    ↓
NORMAL TRAFFIC
```

The pairing and authentication design is recorded in DECISIONS.md (ADR-009, ADR-010) and
SECURITY.md.

---

## 10. Message types in use

Every message uses the envelope in section 3. Fields shown are the payload.

| Type | Direction | Payload |
|---|---|---|
| `PAIR_REQUEST` | phone → PC | `deviceId`, `deviceName`, `proof` (base64 `HMAC-SHA256(code, certFingerprint)`) |
| `PAIR_RESPONSE` | PC → phone | success: `success: true`, `deviceId`, `sharedSecret` (base64), `pcName`; failure: `success: false`, `error` |
| `AUTHENTICATE` | phone → PC | `deviceId`, `nonce`, `timestamp`, `proof` (base64 `HMAC-SHA256(secret, deviceId\|nonce\|timestamp)`) |
| `AUTH_RESULT` | PC → phone | `success`, `error` |
| `NOTIFICATION` | phone → PC | the payload in section 4 |
| `NOTIFICATION_REMOVED` | phone → PC | `notificationId` (accepted, but the PC does not act on it, see ADR-008) |

The certificate fingerprint is the uppercase hex SHA-256 of the PC's certificate. The `AUTHENTICATE`
timestamp must be within 5 minutes of the PC's clock, so both devices need roughly correct time.
