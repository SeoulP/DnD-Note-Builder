# ACTION PLAN — AppLogger (Structured Logging + Toast Notifications)

> **Supersedes:** ErrorService Action Plan

| | |
|---|---|
| **Priority** | High — required before further import/export work |
| **Scope** | New: `Core/AppLogger.cs`, new `Toast` scene; updates to `NavBar.cs`, `App.cs`, `ImportExportModal.cs`, `DatabaseService.cs` |
| **Risk** | Low — additive; existing try/catch blocks are augmented, not restructured |
| **Depends on** | WAL fix (should be merged first) |

---

## Overview

The app currently has no centralised logging. Errors are swallowed silently, printed to the Godot console only, or written to a one-off `backup_debug.log`. There is no record of what the app was doing leading up to a failure, and no way for the user to know something went wrong at runtime.

This plan introduces `AppLogger` — a levelled logger that writes a single persistent `app.log` file to disk and drives a non-blocking toast notification for entries at WARNING and above. Log verbosity is user-configurable so that TRACE/DEBUG output can be enabled when diagnosing issues without a code change or rebuild.

---

## Part 1 — Log levels

Five standard levels in ascending severity order:

| Level | Purpose |
|---|---|
| **TRACE** | Fine-grained process steps ("entering LoadAll", "queried 14 NPCs") |
| **DEBUG** | Diagnostic details useful when investigating behaviour |
| **INFO** | Normal operational events ("campaign opened", "import completed") |
| **WARNING** | Something unexpected but recoverable — surfaces a toast |
| **ERROR** | Something failed and needs attention — surfaces a toast |

The configured **minimum level** acts as a filter. Any entry below the minimum is not written to disk and does not fire the toast event. Default minimum level is `INFO`.

---

## Part 2 — AppLogger (Core layer)

**File:** `Core/AppLogger.cs` — new file, registered as a Godot autoload singleton

### Responsibilities

- Maintains a single append-only `app.log` file in the same directory as `campaign.db`, matching the existing `backup_debug.log` pattern
- Filters entries below the configured minimum level
- Fires a C# event for WARNING and above so the UI layer can show a toast — keeps the Core layer free of Godot UI dependencies
- Exposes `AppLogger.Instance` for static-style access from any class without constructor injection

### Public API

| Method | Purpose |
|---|---|
| `Trace(string source, string message)` | Logs at TRACE level |
| `Debug(string source, string message)` | Logs at DEBUG level |
| `Info(string source, string message)` | Logs at INFO level |
| `Warn(string source, string message)` | Logs at WARNING level; fires `ToastRequested` |
| `Error(string source, string message, Exception ex = null)` | Logs at ERROR level; fires `ToastRequested` |
| `void SetLogDirectory(string dir)` | Called from `DatabaseService._Ready()` once `DbPath` is known |
| `void SetMinLevel(LogLevel level)` | Updates the active filter; called from Settings |
| `LogLevel MinLevel { get; }` | Current minimum level — used to populate the Settings UI |
| `void ClearLog()` | Deletes `app.log` from disk |
| `bool LogExists()` | Returns whether `app.log` exists — drives the disabled state of Clear Log |
| `event Action<string, LogLevel> ToastRequested` | Subscribed to by `App.cs`; passes `(displayMessage, level)` |

### Log file format

Plain text, append-only, one blank line between entries:

```
[2026-03-24 14:32:01Z] [INFO] CampaignDashboard: Campaign opened (id=3)

[2026-03-24 14:32:05Z] [WARNING] ImportExport: Species "Halfling" already exists, skipping

[2026-03-24 14:32:06Z] [ERROR] ImportExport: Failed to parse .dndx file
  System.Text.Json.JsonException: Unexpected token at position 0
    at System.Text.Json.JsonSerializer.Deserialize ...
```

Entries below the current minimum level are not written. If the minimum level is raised after some entries exist, the existing file is not modified — the filter only affects new writes.

### Initialisation

`DatabaseService._Ready()` resolves `DbPath`. Immediately after, call:

```
AppLogger.Instance.SetLogDirectory(Path.GetDirectoryName(DbPath));
AppLogger.Instance.SetMinLevel(/* read from SettingsRepository */);
```

The minimum level is persisted via the existing `SettingsRepository` using a new key `"log_level"` with a default value of `"INFO"`.

---

## Part 3 — Toast UI component

**Files:**
- `Scenes/Components/Toast/Toast.cs` — new file
- `Scenes/Components/Toast/toast.tscn` — new scene

### Behaviour

- Anchored to the bottom-centre of the screen via a `CanvasLayer`, rendering above all other UI
- Appears with a short fade-in; auto-dismisses after ~4 seconds with a fade-out
- If a second toast fires while one is visible, the existing one is replaced immediately — no queue
- No interaction required; never blocks input

### Severity styling

Three visual states, all driven by `ThemeManager` colours so they respect the user's current hue and dark/light mode:

| Level | Background | Icon |
|---|---|---|
| **ERROR** | Red/warm accent | `✕` |
| **WARNING** | Amber/yellow accent | `⚠` |
| **INFO** | Neutral/muted (reserved for future success confirmations) | `ℹ` |

`Toast.Show(string message, LogLevel level)` switches its `PanelContainer` StyleBox and icon label based on the level. All three StyleBox variants are defined as constants on the Toast node.

### Scene structure

```
Toast (CanvasLayer)
  └── Panel (PanelContainer, anchored bottom-centre)
        └── HBoxContainer
              ├── Icon (Label)
              └── Message (Label)
```

---

## Part 4 — Wiring (App.cs)

`App.cs` bridges `AppLogger` to the Toast. On `_Ready()`:

- Instantiate the Toast scene and add it as a child
- Subscribe to `AppLogger.Instance.ToastRequested`
- In the handler, call `_toast.Show(displayMessage, level)`

All other classes only call `AppLogger.Instance.Error/Warn/Info/etc.` — the UI consequence is handled entirely in `App.cs`.

---

## Part 5 — Settings integration (NavBar.cs)

Two additions to the settings popup:

### Log level selector

Add a submenu or inline option button beneath the existing `Remember Tabs` toggle. Label: `"Log Level"`. Populated with the five level names. Current value read from `AppLogger.Instance.MinLevel` on popup open. On change, call `AppLogger.Instance.SetMinLevel(selected)` and persist to `SettingsRepository` via key `"log_level"`.

### Clear Log

Add `"Clear App Log"` below the Backup/Restore items. Disabled when `AppLogger.Instance.LogExists()` returns false — refresh this state by hooking `AboutToPopup` on the settings popup (same pattern needed for dynamic disabled states generally).

On press, show a `ConfirmationDialog` ("This will permanently delete the app log. Continue?"), then on confirm call `AppLogger.Instance.ClearLog()`. After clearing, `AppLogger` writes a first INFO entry: `"Log cleared by user"`. Update the menu item disabled state after clearing.

---

## Part 6 — Migrating existing try/catch blocks

### NavBar.cs — `ShowImportCampaignModal`

Currently:
```csharp
catch { GD.PrintErr("ImportExportModal: failed to parse .dndx file"); return; }
```

Replace with:
```csharp
catch (Exception ex)
{
    AppLogger.Instance.Error("ImportExport", "Failed to parse .dndx file", ex);
    return;
}
```

### NavBar.cs — backup Callable

The existing `backup_debug.log` write can be removed. Replace the catch branch with `AppLogger.Instance.Error(...)` and the success path with `AppLogger.Instance.Info(...)`. The `backup_debug.log` one-off is deprecated once this is in place.

### ImportExportService.ApplyPackage

`ApplyPackage` currently has no error handling. Wrap the body in a try/catch routing to `AppLogger.Instance.Error(...)`. Since `ApplyPackage` is static, it calls `AppLogger.Instance` directly. Add `Info` calls at the start and end of the method to bracket the operation in the log.

### Future call sites

Any new service or repository method that can meaningfully fail at runtime (file I/O, JSON parse, image load) should follow the pattern: catch the specific exception, call `AppLogger.Instance.Error(source, message, ex)`, and handle gracefully. Process milestones (campaign opened, session saved) should call `AppLogger.Instance.Info(...)`.

---

## Out of Scope (for this plan)

- In-app log viewer (surfacing `app.log` contents inside the app)
- Log rotation or size capping
- Surfacing the log file path to the user in the UI
