# Protocol Specification

## Status

Draft. This document defines the direction; implementation must validate the details against the chosen transport and security model.

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

Define hard maximums.

Example starting points for discussion:

```text
app name:       256 chars
title:          1024 chars
body:           8192 chars
expanded lines: 100 entries
line length:    4096 chars
message:        bounded by protocol frame limit
```

These are not final requirements.

The implementation should choose limits based on Android behavior and practical UI constraints.

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

The exact pairing/authentication protocol requires a dedicated security review before implementation.
