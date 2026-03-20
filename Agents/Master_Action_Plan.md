# TTRPG Companion App — Master Action Plan
*March 2026 · v1.0 · Consolidated from Model, UI, Session Feedback, and Navigation/UX plans*

---

## Guiding Rules

> **No destructive migrations.** The schema is frozen for tables with existing data. All changes must be additive: new columns with defaults, new join tables, new tables only.
>
> **Types are seeded, not hardcoded.** All "type" fields use per-campaign seeded models, not enums. Users can add, rename, and delete entries.
>
> **Fix before building.** All bugs and compile errors are resolved before new features.

---

## Status Key

| Symbol | Meaning |
|--------|---------|
| ✅ | Complete |
| ⬜ | To do |
| 🔶 | Partial / in progress |
| 🚫 | Parked / no decision yet |

---

## Open Task Summary

*Completed tasks are summarised in the [Completed Work Log](#completed-work-log) at the bottom.*

| # | Task | Category | Priority | Status |
|---|------|----------|----------|--------|
| U1 | WikiNotes scroll-jump on first click to edit | UX | High | ✅ |
| U2 | EntityRow hover — show pointer cursor | UX | Low | ✅ |
| U3 | Background colour — investigate Godot Themes | UX | Low | ⬜ |
| F1 | Standardize image save location to `imgs/` folder | Feature | High | ⬜ |
| F2 | Per-tab back/forward navigation | Feature | Medium | ⬜ |
| F3 | Session related-links panel (wiki panel, third column) | Feature | Medium | ⬜ |
| F4 | NPC detail pane — full field audit (HomeLocationId, FirstSeenSession, Personality) | Feature | Medium | 🔶 |
| F5 | Campaign Settings screen — manage seeded types | Feature | Medium | ⬜ |
| F6 | Nested locations in sidebar | Feature | Low | ⬜ |
| F7 | Campaign cover image | Feature | Low | ⬜ |
| F8 | Players section — party overview | Feature | Medium | ⬜ |
| F9 | PC abilities / class features | Feature | High | ⬜ |
| F10 | Image export/import in `.dndx` packages | Feature | Medium | ⬜ |
| F11 | NPC–Location relationship | Design | Medium | 🚫 |
| F12 | Session detail pane — significant redesign | Design | High | ⬜ |
| F13 | Tab system for the detail pane | Design | Planned | ⬜ |
| F14 | Remember last opened entity per campaign | Feature | Low | ⬜ |
| F15 | NPC–NPC relationships — directional UI | Feature | Medium | ⬜ |
| F16 | Existing campaigns: check-and-insert missing relationship seeds | Schema | Medium | ⬜ |

---

## Bugs

*No open bugs.*

---

## UX Polish

### U1 — WikiNotes Scroll-Jump Fix

**Root cause:** When clicking the `RichTextLabel` renderer to enter edit mode, `_input.GrabFocus()` causes Godot to scroll the nearest `ScrollContainer` ancestor to bring the focused `TextEdit` into view. Because `_input` is near the top of the `VBoxContainer`, the scroll resets to the top.

**Fix — `Scenes/Components/WikiNotes/WikiNotes.cs`:**

Replace the `_renderer.GuiInput` handler:
```csharp
_renderer.GuiInput += (InputEvent e) =>
{
    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
    {
        var scroll = FindParentScrollContainer();
        int savedScroll = scroll?.ScrollVertical ?? 0;

        _input.Visible    = true;
        _renderer.Visible = false;
        UpdateInputHeight();
        _input.GrabFocus();

        if (scroll != null)
            CallDeferred(MethodName.RestoreScroll, scroll, savedScroll);
    }
};
```

Add the restore method:
```csharp
private void RestoreScroll(ScrollContainer scroll, int savedScroll)
{
    if (IsInstanceValid(scroll))
        scroll.ScrollVertical = savedScroll;
}
```

`FindParentScrollContainer()` already exists — no changes needed there. `CallDeferred` ensures the restore runs after Godot's internal focus-scroll fires. Fix applies to all panes that use `WikiNotes`, not just sessions.

---

### U2 — EntityRow Pointer Cursor

`EntityRow` is a `PanelContainer` whose `MouseDefaultCursorShape` defaults to `Arrow`. The flat `Button` overlay covers most of the row and already shows the pointer, but the root node shows the arrow in gaps and edge cases.

**Fix — `Scenes/Components/EntityRow/EntityRow.cs`, one line at the top of `_Ready()`:**
```csharp
MouseDefaultCursorShape = CursorShape.PointingHand;
```

---

### U3 — Background Colour: Investigate Godot Themes

Colours are currently hardcoded per-component throughout the codebase. Any visual overhaul would require hunting through every file.

**Recommended approach:** Investigate Godot `.tres` theme resources before any visual work. A single theme file defines colours, fonts, and StyleBoxes applied automatically to all matching Control nodes. Do this before touching individual component colours.

---

## Features

### F1 — Standardize Image Save Location to `imgs/` Folder

Currently `entity_images.path` stores whatever absolute path the user picked. This is machine-specific, breaks on migration, and blocks image export (F10).

**Plan:** When the user picks or drops an image, copy it into a managed folder before recording the path:
```
OS.GetUserDataDir()/imgs/<entity_type>/<entity_id>/<original_filename>
```
Store only the relative path (e.g. `imgs/npc/42/portrait.png`) in the DB. On load, resolve to absolute using `OS.GetUserDataDir()`.

**Backwards compatibility:** If a stored path starts with `/` or a drive letter, treat it as a legacy absolute path — do not break existing images.

**Files:**
- `ImageCarousel.cs` — copy file to managed path on pick/drop; store relative path
- `EntityImageRepository.cs` — no schema change; path convention only
- All image display code — resolve relative paths before loading texture

**Folder structure:**
```
%APPDATA%/Godot/app_userdata/dnd-builder/
  campaign.db
  imgs/
    npc/42/portrait.png
    location/7/map.jpg
```

> **Prerequisite for F10 (image export/import).**

---

### F2 — Per-Tab Back/Forward Navigation

**New file: `Scenes/Components/TabHistory.cs`**
```csharp
public class TabHistory
{
    private readonly List<(string EntityType, int EntityId)> _stack = new();
    private int _index = -1;

    public bool CanGoBack    => _index > 0;
    public bool CanGoForward => _index < _stack.Count - 1;
    public (string EntityType, int EntityId) Current => _stack[_index];

    public void Push(string entityType, int entityId)
    {
        if (_index >= 0 && _stack[_index] == (entityType, entityId)) return;
        _stack.RemoveRange(_index + 1, _stack.Count - _index - 1);
        _stack.Add((entityType, entityId));
        _index++;
    }

    public (string EntityType, int EntityId) Back()    { if (CanGoBack)    _index--; return Current; }
    public (string EntityType, int EntityId) Forward() { if (CanGoForward) _index++; return Current; }
}
```

**`CampaignDashboard.cs` changes:**
- Add `private TabHistory _history = new();` (non-readonly — needs reset on campaign switch).
- Add `[Export] private Button _backButton` and `[Export] private Button _forwardButton`.
- Split `ShowDetailPane` into `NavigateToInternal(string entityType, int entityId, bool pushHistory)`.
  - `pushHistory: true` → `_history.Push(...)` then `RefreshNavButtons()`.
  - `pushHistory: false` → skip push (used by back/forward traversal).
- `_backButton.Pressed` → `NavigateToInternal(_history.Back(), pushHistory: false)`.
- `_forwardButton.Pressed` → `NavigateToInternal(_history.Forward(), pushHistory: false)`.
- `RefreshNavButtons()` sets `Disabled` from `_history.CanGoBack` / `CanGoForward`.
- Reset `_history` in `SetCampaign()`.

**Mouse side-buttons — `_UnhandledInput` in `CampaignDashboard`:**
```csharp
public override void _UnhandledInput(InputEvent e)
{
    if (e is InputEventMouseButton { Pressed: true } mb)
    {
        if (mb.ButtonIndex == MouseButton.Xbutton1 && _history.CanGoBack)
        { NavigateToInternal(_history.Back(), pushHistory: false); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Xbutton2 && _history.CanGoForward)
        { NavigateToInternal(_history.Forward(), pushHistory: false); AcceptEvent(); }
    }
}
```

**`CampaignDashboard.tscn`:** Add a small `HBoxContainer` with `←` and `→` flat buttons above or inset into `_detailPanel`. Wire exports.

---

### F3 — Session Related-Links Panel

In `session_detail_pane.tscn`, the `CarouselColumn` (`VBoxContainer`) on the right is the wiki panel. Below the `ImageCarousel`, add a `RelatedLinksContainer` (`VBoxContainer`) that displays all `[[WikiLink]]` names found in the session notes as clickable navigation buttons.

**`SessionDetailPane.cs` — add:**
```csharp
private List<(string Name, string EntityType, int EntityId)> ParseSessionLinks()
{
    var results = new List<(string, string, int)>();
    if (_session == null || _db == null) return results;
    var lookup = BuildReverseLookup(_session.CampaignId);
    var matches = Regex.Matches(_session.Notes ?? "", @"\[\[([^\]]+)\]\]");
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (Match m in matches)
    {
        string name = m.Groups[1].Value;
        if (!seen.Add(name)) continue;
        if (lookup.TryGetValue(name.ToLowerInvariant(), out var entry))
            results.Add((name, entry.EntityType, entry.EntityId));
    }
    return results;
}

private Dictionary<string, (string EntityType, int EntityId)> BuildReverseLookup(int campaignId)
{
    var d = new Dictionary<string, (string, int)>();
    foreach (var x in _db.Npcs.GetAll(campaignId))      d[x.Name.ToLowerInvariant()]  = ("npc",      x.Id);
    foreach (var x in _db.Factions.GetAll(campaignId))  d[x.Name.ToLowerInvariant()]  = ("faction",  x.Id);
    foreach (var x in _db.Locations.GetAll(campaignId)) d[x.Name.ToLowerInvariant()]  = ("location", x.Id);
    foreach (var x in _db.Sessions.GetAll(campaignId))  d[x.Title.ToLowerInvariant()] = ("session",  x.Id);
    return d;
}

private void RefreshRelatedLinks()
{
    if (_relatedLinksContainer == null) return;
    foreach (Node child in _relatedLinksContainer.GetChildren()) child.QueueFree();
    var links = ParseSessionLinks();
    if (links.Count == 0) { _relatedLinksContainer.Visible = false; return; }
    _relatedLinksContainer.Visible = true;
    var header = new Label { Text = "Referenced" };
    header.AddThemeFontSizeOverride("font_size", 13);
    header.Modulate = new Color(0.7f, 0.7f, 0.7f);
    _relatedLinksContainer.AddChild(header);
    foreach (var (name, entityType, entityId) in links)
    {
        string eName = name; string eType = entityType; int eId = entityId;
        var btn = new Button { Text = eName, Flat = true, Alignment = HorizontalAlignment.Left,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.Pressed += () => EmitSignal(SignalName.NavigateTo, eType, eId);
        _relatedLinksContainer.AddChild(btn);
    }
}
```

Call `RefreshRelatedLinks()` at end of `Load()` and on `_notes.TextChanged`.

**`session_detail_pane.tscn`:** Add `RelatedLinksContainer` VBoxContainer under `CarouselColumn`, after `ImageCarousel`. Remove or reposition `CarouselSpacer`. Wire export.

---

### F4 — NPC Detail Pane: Full Field Audit 🔶 Partial

Status and RelationshipTypeId dropdowns are done. Still missing:

| Field | Source | Action |
|-------|--------|--------|
| `HomeLocationId` | `Npc` | Add Location dropdown to NPC pane |
| `FirstSeenSession` | `Npc` | Add Session dropdown to NPC pane |
| `Personality` | `Character` | Verify present in pane and wired |

---

### F5 — Campaign Settings Screen: Manage Seeded Types

Five per-campaign tables need UI for add/rename/delete. All five screens are structurally identical.

**Recommended approach:** One `CampaignSettingsModal` scene with a `TabContainer`. Each tab is an instance of a shared `TypeEditorPanel` subscene (list + name/description inputs + Add/Delete buttons). Each tab is wired to a different repository.

**Tables to manage:**

| Tab | Repository | Table |
|-----|------------|-------|
| Species | `SpeciesRepository` | `species` |
| NPC Statuses | `NpcStatusRepository` | `npc_statuses` |
| NPC Relationships | `NpcRelationshipTypeRepository` | `npc_relationship_types` |
| Faction Roles | `LocationFactionRoleRepository` | `location_faction_roles` |
| Item Types | `ItemTypeRepository` | `item_types` |

> Do not allow deleting a type if any records reference it — warn the user before deletion.

---

### F6 — Nested Locations in Sidebar

The data layer is complete: `LocationRepository.GetTopLevel()` and `GetChildren()` already exist.

**Change:** In `CampaignDashboard.LoadLocations()`, replace the flat `GetAll()` call with a recursive tree using `GetTopLevel()` + `GetChildren()`. Indent child buttons visually with left padding or use Godot's `Tree` control.

> Ship together with the Parent Location picker in `LocationDetailPane` — they are the same feature from two angles.

---

### F7 — Campaign Cover Image

Add `Campaign` to the `EntityType` enum in `EntityType.cs`. Everything else (`EntityImageRepository`, `ImageCarousel`) already works generically for any entity type. No other data layer changes required.

---

### F8 — Players Section: Party Overview

`PlayerCharacter.cs` model exists. Full PC mechanics are deferred to F9.

**Scope:**
- Add a Players accordion to `CampaignDashboard` alongside the existing sections.
- Detail pane shows: name, species, class, level — basic identifying information only.

---

### F9 — PC Abilities / Class Features

**New `abilities` table:**

| Column | Type | Notes |
|--------|------|-------|
| `id` | INTEGER PK | |
| `player_character_id` | INTEGER FK | → `player_characters(id)` |
| `name` | TEXT | |
| `type` | TEXT | Class Feature, Maneuver, Feat, etc. |
| `action` | TEXT | Action / Bonus Action / Reaction / Passive / Free |
| `trigger` | TEXT | Optional — e.g. "When you miss an attack" |
| `cost` | TEXT | Optional — e.g. "1 Superiority Die" |
| `uses` | INTEGER | Max uses (0 = unlimited) |
| `uses_remaining` | INTEGER | Current remaining uses |
| `recovery` | TEXT | e.g. "Short Rest", "+1 per Short Rest" |
| `effect` | TEXT | Full description |
| `notes` | TEXT | Rulings and reminders |
| `sort_order` | INTEGER | Display order within character sheet |

---

### F10 — Image Export/Import in `.dndx` Packages

**Dependency: F1 must be complete first.** Without a managed `imgs/` folder there is no reliable way to find image files during export.

**Approach:** Embed images as base64 in the JSON. Each exported entity image becomes a record in `ExportPackage.Images`:
- `EntityType`, `OldEntityId`, `Filename`, `Data` (base64 bytes)

On import: decode base64 → write file to `imgs/<entity_type>/<new_entity_id>/<filename>` → insert `entity_images` row with remapped entity ID.

**Files:** `ExportPackage.cs`, `ImportExportService.BuildPackage()`, `ImportExportService.ApplyPackage()`, `ImportExportModal.cs` (optional "Include Images" checkbox).

**Alternative (deferred):** Package `.dndx` as a `.zip` containing `data.json` + `imgs/` folder. More efficient for large images but requires a zip library. Defer unless base64 file sizes become a problem.

---

### F11 — NPC–Location Relationship 🚫 No Decision

A single location field on an NPC conflates three distinct concepts:

| Concept | Description |
|---------|-------------|
| Home / Origin | Where they are from or based. Stable, set during prep. |
| Current Location | Where they are right now. Changes during play. |
| Last Seen | Session-stamped observation — "As of Session 12, spotted in Waterdeep." |

**Options under consideration:**
1. Two optional fields on `Npc`: `HomeLocationId` (FK already exists) + `LastSeenNote` (freetext with optional session reference).
2. A join table `npc_locations` with `location_id`, `session_id`, `note` — a full history.

> Option 1 is simpler and covers most DM use cases. Option 2 is more powerful but adds complexity before the need is proven. No decision yet — revisit when building the NPC pane update (F4).

---

### F12 — Session Detail Pane: Significant Redesign

The current pane has: number label, title, played-on date, wiki notes, image carousel, delete. Most-used pane during active play; most underdeveloped.

**Missing:**
- Entity tagging panel — structured fields for which NPCs, locations, factions, and quests appeared
- Inline stub creation — create a new NPC or location from within the session pane without losing place
- Wiki link hover preview — hovering a `[[Link]]` shows a preview without navigating
- Quest status updates — log progress on a quest from within session notes

> Plan together with F13 (Tab system) — they are the same body of work.

---

### F13 — Tab System for the Detail Pane

**Behaviour:**
- One tab per record; strict deduplication — navigating to an open record switches to its tab
- Navigation priority: existing tab → current unpinned tab → next unpinned tab → new tab
- Pinned tabs skipped by navigation

**Tab bar:**
- Scrolls horizontally; auto-scrolls to keep active tab visible
- `+` button fixed at end for explicit new tab creation
- Middle-click or hover-× to close

**Files:** New `TabBar` component. Integration into `CampaignDashboard` — detail panel becomes tab-managed.

> The per-tab history (F2) is implemented now against the existing single-pane architecture and migrates naturally when tabs land.

---

### F14 — Remember Last Opened Entity Per Campaign

When reopening a campaign, restore the last viewed entity (type + id) so the detail pane is shown immediately rather than landing on a bare sidebar.

---

### F15 — NPC–NPC Relationships: Directional UI

`NpcRelationship` model and repository exist. The `CharacterRelationshipTypeRepository` has been seeded including "Acquainted with."

**Open question:** Relationships are directional in the model (from/to NPC). "Sildar is Acquainted with Iarno" does not automatically imply the reverse. The UI for displaying and editing both directions is not yet designed.

**Recommendation:** Display both directions as separate records. Confirm direction behaviour before building.

---

### F16 — Existing Campaigns: Check-and-Insert Missing Relationship Seeds

`SeedDefaults` only runs on campaign creation. Existing campaigns do not receive new seeds automatically.

**Fix:** On campaign load, for each default relationship type, if no record with that name exists for this `campaign_id`, insert it. Apply to: `CharacterRelationshipTypeRepository`, `NpcRelationshipTypeRepository`, `NpcStatusRepository`, `LocationFactionRoleRepository`.

---

## Implementation Order

### Short-term
5. **F3 — Session related-links panel** — self-contained, no schema changes.
6. **F16 — Seed check-and-insert on campaign load** — additive, safe.
7. **F4 — NPC pane field audit** — finish the partial work.

### Medium-term
8. **F1 — Standardize image save location** — prerequisite for F10.
9. **F5 — Campaign Settings screen** — one generic screen, five type tables.
10. **F8 — Players section** — basic party overview.
11. **F2 — Back/forward navigation** — largest navigation change; do after simpler fixes are stable.

### Planned / Larger scope
12. **F6 — Nested locations** — ship with parent location picker.
13. **F9 — PC abilities / class features** — new table, greenfield.
14. **F12 + F13 — Session redesign + Tab system** — plan together.
15. **F10 — Image export/import** — requires F1.
16. **F7 — Campaign cover image** — one enum entry; trivial but low priority.
17. **F14 — Remember last opened entity** — quality-of-life, low risk.
18. **U3 — Theme investigation** — prerequisite for any visual overhaul.

### Blocked / No decision
- **F11 — NPC–Location relationship** — blocked on design decision.
- **F15 — NPC–NPC relationship UI** — blocked on direction behaviour decision.

---

## File Change Summary

| File | Change | Task |
|------|--------|------|
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Magic-byte format detection + raw byte loading; OS drop via `Window.FilesDropped` | B1, B2 |
| `Scenes/Components/ImageLightbox/ImageLightbox.cs` | Magic-byte format detection + raw byte loading | B1 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Fix `_renderer.GuiInput` handler; add `RestoreScroll()` | U1 |
| `Scenes/Components/EntityRow/EntityRow.cs` | Add `MouseDefaultCursorShape = CursorShape.PointingHand` in `_Ready()` | U2 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Copy image to managed path on add; store relative path | F1 |
| `Scenes/Components/TabHistory.cs` | **New** — `TabHistory` helper class | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Add history, back/forward buttons, `NavigateToInternal`, `_UnhandledInput`, `RefreshNavButtons()` | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | Add `←` / `→` buttons; wire exports | F2 |
| `Scenes/Components/SessionDetailPane/SessionDetailPane.cs` | Add `ParseSessionLinks()`, `BuildReverseLookup()`, `RefreshRelatedLinks()`; update `_notes.TextChanged` | F3 |
| `Scenes/Components/SessionDetailPane/session_detail_pane.tscn` | Add `RelatedLinksContainer` under `CarouselColumn`; remove/reposition `CarouselSpacer` | F3 |
| `Scenes/Components/NpcDetailPane/NpcDetailPane.cs` + `.tscn` | Add HomeLocationId, FirstSeenSession, verify Personality | F4 |
| `Core/Repositories/*RelationshipTypeRepository.cs` et al. | Add check-and-insert on campaign load for new seeds | F16 |

---

## Completed Work Log

All items below are done and require no further action unless noted.

### Model (pre-alpha)
- ✅ `Faction.Headquarters` removed
- ✅ `Location.FactionIds` → `LocationFaction` join model
- ✅ `LocationFactionRole` seeded model created
- ✅ `NpcRelationship` enum → `NpcRelationshipType` seeded model
- ✅ `Npc` refactored into `Character` base + `Npc` + `PlayerCharacter` (Table-Per-Type)
- ✅ `NpcStatus` enum → `NpcStatus` seeded model
- ✅ Item system: `Item`, `ItemType`, `SystemItem`, `DnD5eItemMechanics`, `CharacterItem`, `LocationItem`

### UI / Compile fixes
- ✅ `FactionDetailPane` — Headquarters removed
- ✅ `LocationDetailPane` — `FactionIds` → `Factions.Select(f => f.FactionId)`
- ✅ `NpcDetailPane` — enum dropdowns replaced with DB-loaded dropdowns (Status + RelationshipType)
- ✅ Item list + detail pane
- ✅ Entity image carousel system (`ImageCarousel`, `ImageLightbox`, `EntityImageRepository`, all five detail panes wired)

### UX Polish (2026-03-19)
- ✅ U2 — EntityRow pointer cursor: `MouseDefaultCursorShape = CursorShape.PointingHand` on `navBtn` and `delBtn` inside `EntityRow._Ready()`.
- ✅ U1 — WikiNotes click-to-edit fully resolved: scroll position saved/restored around `GrabFocus`; caret placed at click position via `GetLineColumnAtPos`; `ScrollFitContentHeight = true` prevents scrollbar flicker; fonts/sizes matched between `TextEdit` and `RichTextLabel` via theme API; renderer stylebox tuned to match focused TextEdit layout (left margin = `focusSb.ContentMarginLeft`, top margin = `focusSb.ContentMarginTop + focusSb.BorderWidthTop * 2` to compensate for the 2 px layout shift when focus border activates); `caret_blink = true` added to `wiki_notes.tscn`.

### Image loading (2026-03-19)
- ✅ B1 — Image load bug fixed: `ImageCarousel` and `ImageLightbox` now read raw bytes via `System.IO.File.ReadAllBytes` and detect format from magic bytes (PNG/JPEG/WebP), bypassing Godot's extension-based loader. Handles files with mismatched extensions (e.g. WebP saved as .png).
- ✅ B2 — Drag-and-drop now works via `Window.FilesDropped` signal. Godot 4 does not route OS file drops through `_CanDropData`/`_DropData` — those are internal-only. Drop hover overlay was investigated and dropped: Godot 4 exposes no OS drag hover events, so reliable visual feedback during drag is not possible without platform-specific hooks.

### Session feedback
- ✅ Spacebar opens image importer from lightbox — fixed via `GetViewport().GuiReleaseFocus()` in `OpenLightbox()`
- ✅ Session sidebar — title only, no index prefix
- ✅ Wasted space left/right of app — `app.tscn` margins corrected; `SetMargins()` in `App.cs`
- ✅ Import / Export — Backup, Restore, Export Campaign Data, Import Campaign Data; `.dndx` JSON format
- ✅ All text areas auto-expand in edit mode (`WikiNotes`)
- ✅ Entity name fields select-all on focus (all detail panes)
- ✅ Lightbox cursor — pointer hand on carousel, Move cursor in lightbox
- ✅ OS-native file dialog (`DisplayServer.FileDialogShow()`)
- ✅ Navbar built out — campaign name, back button, settings menu
- ✅ "Acquainted with" NPC relationship seed added
- ✅ Notes last in all detail panes
- ✅ Detail pane footer — 24px breathing room at window bottom
- ✅ New sessions always append to end of sidebar list
- ✅ Settings menu tooltips
- ✅ Three-column layout (sidebar / detail / wiki panel)
- ✅ Quests entity + Quest History sub-feature
- ✅ NPC–NPC relationships (model + UI; direction design note in F15)

---

*Generated March 2026 · TTRPG Companion App · Master Action Plan · v1.0*
