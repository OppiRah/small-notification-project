# Requirements — Must / Should / Must-Not

Concrete requirements the implementation is judged against. See `PRODUCT.md` for the user-facing
experience these requirements exist to deliver, and `UI_UX.md` for the visual/interaction spec.
`README.md`'s "Non-goals for MVP" list is the top-level scope boundary; this doc doesn't repeat
it, only the requirements that follow from it.

---

## 1. Notification content

**Must:** the Android implementation extracts all useful content that Android exposes for the
notification.

Potential data includes:

- app/package identity
- title
- text
- big text
- messaging-style content
- inbox-style lines
- summary
- subtext
- timestamp
- category
- notification key/id
- icon
- conversation-related information where accessible

### Important limitation

**Must not:** promise that the bridge can recover hidden/private notification content. The
bridge cannot reveal content that the originating application does not expose through the
Android notification.

---

## 2. Privacy

The product displays potentially sensitive content.

**Must (default behavior):**

- do not persist bodies
- do not write full bodies to normal logs
- do not send data through the cloud
- do not collect analytics
- do not transmit notification data anywhere except the paired PC
- do not retain notification history in MVP

**Should:** debug mode redacts notification bodies unless the developer explicitly opts into
verbose logging.

---

## 3. Settings

### MVP — must have

**Connection**

- paired device status
- connection status
- pairing/unpairing
- network/endpoint status where appropriate

**Display**

- monitor
- position
- bubble duration
- animation enabled/disabled
- max visible bubbles

**Privacy**

- notification history: OFF / unavailable in MVP
- debug logging
- log redaction

*As built:* there is no user-facing debug-logging switch. Instead, notification text is never
logged at all: the Android app logs metadata only (a code-level `BridgeLogger.verbose` flag exists,
defaults to off, and has no caller), and the Windows Developer tab logs only message type and
package name.

**Startup**

- start with Windows

### Future — explicitly out of MVP scope

- per-app filters
- do-not-disturb schedule
- notification grouping
- click actions
- sound
- theme
