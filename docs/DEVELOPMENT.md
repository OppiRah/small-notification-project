# Development Rules — Claude Code

This is the single operating doc for whoever implements the **Phone Notification Bridge**
project — now just Claude Code, working end to end: research, validation, planning, and
implementation in one continuous session. (This file previously had a companion,
`CLAUDE_HANDOFF.md`, written for a separate planning-stage agent that handed a plan off to
Claude Code; that split is gone, and this doc absorbs everything from it that still applies.)

## Role

Claude Code acts as the project's senior engineer and implementation planner.

The project owner remains responsible for approving architecture and behavior. Claude Code does
not blindly accept the existing docs as final — they are a strong starting proposal that must be
technically validated against current platform behavior before anything is built on top of them.

Do not blindly implement ambiguous requirements.

---

## Before implementation

Read every Markdown doc in this project:

- README.md
- PRODUCT.md
- REQUIREMENTS.md
- UI_UX.md
- ARCHITECTURE.md
- PROTOCOL.md
- SECURITY.md
- ROADMAP.md
- TESTING.md
- DECISIONS.md

Then identify:

1. known requirements
2. assumptions
3. unresolved decisions
4. platform constraints
5. security implications

---

## First task (before writing any production code)

1. Research current official Android documentation for `NotificationListenerService` and
   notification extras/styles.
2. Research the current recommended Windows desktop stack for the target Windows environment.
3. Evaluate WebSocket vs. another local transport.
4. Design a secure pairing/authentication approach using established libraries/primitives —
   do not invent cryptography.
5. Identify Android background/service lifecycle limitations.
6. Identify Windows overlay/window-management limitations.
7. Identify notification types that cannot or should not be mirrored.
8. Produce a technical implementation plan.
9. Explicitly list every item under "Unresolved decisions" in DECISIONS.md, with a
   recommendation and reasoning for each, for the project owner's approval.

---

## Decision policy

If an architectural decision is genuinely unresolved — including anything listed under
"Unresolved decisions" in DECISIONS.md, or anything discovered during research that the docs
don't already settle — **stop and surface it explicitly before implementing against it.**
Propose a recommendation, but wait for the project owner's sign-off rather than picking silently
and moving on. This applies even when Claude Code is confident in the right answer.

If a requirement conflicts with platform constraints discovered during research, explain the
conflict and propose alternatives instead of silently implementing a broken approximation.

---

## Critical constraints

- Phone is the sole notification source.
- No cloud dependency.
- Local-first.
- Notification content is sensitive.
- No unauthenticated LAN notification receiver.
- No per-app integrations for MVP.
- Do not build reply/remote-control/AI features in MVP.
- Do not persist notification bodies in MVP.
- Do not steal keyboard/mouse focus merely to display a notification.
- Do not assume every notification exposes full content.
- Do not assume monitor coordinates are positive.
- Do not overengineer.

---

## Implementation philosophy

Build in vertical slices:

1. Android notification extraction
2. Windows synthetic receiver
3. protocol
4. Android → Windows transport
5. bubble rendering
6. monitor selection/placement
7. pairing/security
8. reliability
9. settings
10. polish

Every slice should have tests and a runnable verification method.

Claude Code should modify the repository only after a slice's plan is sufficiently concrete and
any decisions it depends on have been approved.

---

## Implementation principles

### Keep the system small

Avoid:

- unnecessary microservices
- unnecessary databases
- cloud infrastructure
- dependency-heavy frameworks
- speculative abstractions
- generic frameworks built for imaginary future features

### Preserve separation

- Android notification acquisition should not become a giant service class.
- Windows UI should not contain network parsing.
- Network code should not manipulate window coordinates.
- Notification domain models should not depend directly on UI framework types.

### Research platform behavior

When implementation depends on Android or Windows behavior, verify against official
documentation. Do not assume:

- all Android notifications have the same structure
- all notification content is available
- a notification ID is globally unique
- all monitors use positive coordinates
- topmost guarantees visibility above every application
- background Android behavior is identical across devices
- network connectivity will remain stable

---

## Operating rules

Before modifying files:

1. inspect repository
2. inspect existing architecture
3. inspect relevant platform code
4. identify affected files
5. state implementation plan
6. implement the smallest coherent change
7. run tests/build
8. inspect failures
9. fix actual causes
10. summarize changed files and verification

Do not rewrite unrelated code. Do not introduce a new dependency without justification. Do not
silently change the protocol. Do not weaken security for convenience.

---

## Definition of done

A feature is not done merely because code compiles.

```text
Requirement
    ↓
Implementation
    ↓
Tests
    ↓
Build
    ↓
Manual verification where needed
    ↓
Documentation
```

---

## First physical run (Phase 1 — Android device)

Steps to get the listener service running on a real phone and watch it log real notifications.

1. On the phone: Settings → About phone → tap Build number 7x to unlock Developer Options.
2. Settings → Developer options → enable USB debugging.
3. Connect the phone to the laptop via USB. Accept the "Allow USB debugging?" prompt on the
   phone (check "always allow from this computer" if this is a trusted dev machine).
4. Verify the connection:
   ```text
   adb devices
   ```
   The device should show as `device`, not `unauthorized` or `offline`.
5. Build and install the debug APK:
   ```text
   cd android
   ./gradlew installDebug
   ```
   (use `gradlew.bat` if not running inside a POSIX shell)
6. Grant notification access manually — this cannot be requested programmatically:
   Settings → Apps → Special app access → Notification access → enable for this app.
   Exact path varies by OEM/Android version (some put it under Settings → Notifications →
   "Device & app notifications" or "Notification access").
7. Watch logs filtered to this app's tag:
   ```text
   adb logcat -s NotificationBridge
   ```
8. Trigger notifications from the apps listed in ROADMAP.md's Phase 1 test matrix (messaging,
   social, email, browser, system, media, grouped, ongoing, expanded, multi-message) and confirm
   each one logs a `Notification posted package=... category=...` line.

Notification bodies are not logged by default (`BridgeLogger.verbose` is `false` per
SECURITY.md) — only metadata. Do not flip `verbose` to `true` outside a local debug session.

---

## Git workflow

Because this project is developed with AI assistance:

- make small commits
- keep changes logically grouped
- avoid giant generated commits
- review diffs before committing
- use pull requests when collaborating
- do not commit secrets
- do not commit notification dumps/logs
- do not commit pairing keys
- do not commit generated build artifacts unless intentionally required

Suggested commit style:

```text
feat(android): add notification listener
feat(protocol): add notification message schema
feat(windows): add local receiver
feat(ui): add notification bubble
feat(display): add monitor selection
test(protocol): add malformed-message cases
fix(connection): reconnect after network loss
```

---

## AI-generated code review checklist

For every substantial change, ask:

### Correctness

- Does it actually satisfy the requirement?
- Are platform assumptions verified?
- Are edge cases handled?

### Security

- Can an unauthenticated device inject notifications?
- Are private messages logged?
- Is input validated?
- Are secrets stored safely?

### Reliability

- What happens when the network disappears?
- What happens when the phone restarts?
- What happens when the monitor disappears?
- What happens when 100 notifications arrive rapidly?

### Maintainability

- Can another developer understand this?
- Is the abstraction necessary?
- Is responsibility in the correct layer?

### Testing

- Is the behavior automated where practical?
- Is the failure path tested?
- Is there a regression test?

### Scope

- Did the implementation add features nobody requested?
- Did it introduce unnecessary dependencies?
- Did it solve a future problem instead of the current one?

Report verification honestly — including partial failures.
