# Testing Strategy

## Testing philosophy

The project should not rely on manually checking whether a bubble appeared.

The notification pipeline should be testable in layers.

---

## 1. Android unit tests

Test notification normalization with synthetic notification structures.

Cases:

- title only
- body only
- title + body
- long body
- empty body
- multiple expanded lines
- messaging style
- inbox style
- missing app label
- missing icon
- missing timestamp
- unusual Unicode
- emojis
- very long strings
- duplicate/update notification

---

## 2. Protocol tests

Test:

- valid message
- invalid JSON
- unknown message type
- unsupported version
- missing required field
- oversized field
- oversized message
- invalid timestamp
- unauthenticated notification
- malformed payload

Expected behavior:

```text
reject safely
log minimally
keep connection/application alive when possible
```

---

## 3. Connection tests

Test:

- initial connection
- authentication
- reconnect
- disconnect
- timeout
- connection refusal
- invalid credentials
- duplicate connection
- device unpair
- PC restart
- phone restart

---

## 4. Notification-manager tests

Test:

- one notification
- multiple notifications
- notification expiration
- independent expiration
- update existing notification
- removal
- burst of notifications
- maximum visible count
- overflow behavior

Example:

```text
A arrives
B arrives
A expires
C arrives
B remains
C appears
```

---

## 5. Display tests

Test:

- one monitor
- two monitors
- primary monitor
- secondary monitor
- monitor to left
- monitor to right
- monitor above
- negative monitor coordinates
- different resolutions
- different DPI/scaling
- monitor unplug
- monitor replug
- display order changes

Windows documentation explicitly notes that multi-monitor coordinates can be negative, so tests must not assume all displays begin at `(0,0)`.

---

## 6. UI tests

Verify:

- bubble appears
- bubble disappears
- fade animation
- long text layout
- text wrapping
- icon fallback
- stacking
- hover pause
- no focus stealing
- click behavior

---

## 7. End-to-end acceptance test

Scenario:

1. Start Windows application.
2. Pair Android phone.
3. Select secondary monitor.
4. Trigger notification on phone.
5. Confirm bubble appears on secondary monitor.
6. Confirm full available content is displayed.
7. Trigger three more notifications.
8. Confirm stacking.
9. Wait for independent expiration.
10. Disconnect phone network.
11. Confirm reconnect state.
12. Restore network.
13. Confirm automatic reconnection.
14. Trigger another notification.
15. Confirm normal operation resumes.

---

## 8. Performance expectations

The system is a small utility.

Watch for:

- unnecessary CPU usage while idle
- memory growth after many notifications
- UI thread blocking
- notification bursts causing animation lag
- connection reconnect loops

Do not optimize prematurely.

Measure before changing architecture.
