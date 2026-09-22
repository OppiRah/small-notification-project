# UI/UX — Bubble & Monitor Behavior

Exact visual and interaction spec for the desktop overlay. See `PRODUCT.md` for the surrounding
user experience and `REQUIREMENTS.md` for the settings that expose these behaviors to the user.

---

## 1. Bubble behavior

### Default

- top-right of primary monitor
- always above normal application windows
- no focus stealing
- no taskbar activation
- automatic fade-in
- configurable visible duration
- automatic fade-out
- rounded visual container
- app name
- title/sender
- body/content
- optional icon

### Interaction

Recommended MVP interaction:

- hover pauses dismissal timer
- moving away resumes timer
- click behavior may initially do nothing
- close button may be optional

**Must not:** make clicking a notification open arbitrary applications until the security and
mapping model is explicitly designed.

---

## 2. Monitor behavior

The Windows application must enumerate available displays.

Default:

```text
target monitor = primary monitor
```

User configuration:

```text
Monitor:
- Primary
- Display 2
- Display 3
...
```

**Must not:** assume the primary display is monitor `0`.

Windows multi-monitor layouts can use negative coordinates and nontrivial virtual desktop
arrangements. Use monitor APIs and monitor work areas rather than hard-coded screen dimensions.

The overlay should stay inside the selected monitor's usable work area unless the user
explicitly enables edge-overflow behavior.

---

## 3. Placement

MVP:

- top-left
- top-right
- bottom-left
- bottom-right

Future:

- custom X/Y offset
- drag-to-position preview
- per-monitor placement
- safe-area configuration
