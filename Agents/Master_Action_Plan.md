# TTRPG Companion App — Master Action Plan
*March 2026 · v1.1 · Consolidated from Model, UI, Session Feedback, and Navigation/UX plans*

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
| U3 | Background colour — project-wide Godot Theme | UX | Low | ✅ |
| U4 | TypeOptionButton — auto-select newly added type | UX | Medium | ⬜ |
| U5 | WikiNotes — bullet point continuation on Enter | UX | Low | ⬜ |
| F1 | Standardize image save location to `imgs/` folder | Feature | High | ⬜ |
| F2 | Per-tab back/forward navigation | Feature | Medium | ⬜ |
| F3 | Session related-links panel (wiki panel, third column) | Feature | Medium | ⬜ |
| F4 | NPC detail pane — full field audit (HomeLocationId, FirstSeenSession, Personality) | Feature | Medium | 🔶 |
| F5 | Campaign Settings screen — manage seeded types | Feature | Medium | ⬜ |
| F6 | Nested locations in sidebar | Feature | Low | ✅ |
| F7 | Campaign cover image | Feature | Low | ⬜ |
| F8 | Players section — party overview | Feature | Medium | ⬜ |
| F9 | PC abilities / class features | Feature | High | ⬜ |
| F10 | Image export/import in `.dndx` packages | Feature | Medium | ⬜ |
| F11 | NPC–Location relationship | Design | Medium | 🚫 |
| F12 | Session detail pane — significant redesign | Design | High | 🔶 |
| F13 | Tab system for the detail pane | Design | Planned | ⬜ |
| F14 | Remember last opened entity per campaign | Feature | Medium | ⬜ |
| F15 | NPC–NPC relationships — directionality display | Feature | Medium | ⬜ |
| F16 | Call SeedDefaults on campaign load (+ add God/Worship seeds) | Schema | Medium | ⬜ |
| F17 | WikiNotes — Items and Quests included in WikiLink autocomplete | Feature | High | ⬜ |
| F18 | Session view — quick-create entities without navigating away | Feature | High | ⬜ |

---

## Bugs

*No open bugs.*

---

## UX Polish

### U1 — WikiNotes Scroll-Jump Fix ✅

*(See Completed Work Log)*

---

### U2 — EntityRow Pointer Cursor ✅

*(See Completed Work Log)*

---

### U3 — Background Colour: Project-Wide Godot Theme ✅

*(See Completed Work Log)*

---

### U4 — TypeOptionButton: Auto-Select Newly Added Type

When a user types a new type name and clicks "+ Add", the popup closes and the new type is automatically selected. Currently `AutoSelectOnAdd` defaults to `false` and is only set to `true` on entity-picker dropdowns (e.g. NPC select, Faction select). It is missing from all the "type" dropdowns that save directly to a record.

**Affected call sites — add `AutoSelectOnAdd = true` before each `Setup()` call:**

| File | Field |
|------|-------|
| `NpcDetailPane.cs` | `_speciesInput`, `_statusInput`, `_relationshipInput`, `_roleSelect`, `_relTypeSelect` |
| `ItemDetailPane.cs` | `_typeInput` |
| `QuestDetailPane.cs` | `_statusInput` |
| `FactionDetailPane.cs` | `_roleSelect`, `_relFactionTypeSelect` |
| `LocationDetailPane.cs` | `_roleSelect` |

No changes required to `TypeOptionButton.cs` — the `AutoSelectOnAdd` path already works correctly.

---

### U5 — WikiNotes: Bullet Point Continuation

Typing `- ` (dash + space) at the start of a line should initiate a bullet list. Pressing Enter should continue the list on the next line. Pressing Enter on an empty bullet body (i.e. `- ` with nothing after it) should remove the bullet prefix and return to normal text.

**Applies to:** the `TextEdit` (`_input`) inside `WikiNotes.cs`, and by extension any WikiNotes instance — including WikiLink reference panels.

**Implementation — `WikiNotes.cs`, `OnInputKey()`:**

The handler already intercepts `InputEventKey` for autocomplete navigation. Extend it to intercept `Key.Enter` / `Key.KpEnter` when the autocomplete panel is not visible:

```csharp
case Key.Enter:
case Key.KpEnter:
    if (_acPanel != null && IsInstanceValid(_acPanel) && _acPanel.Visible)
    {
        ConfirmSelection();
        _input.AcceptEvent();
        break;
    }
    if (HandleBulletContinuation())
        _input.AcceptEvent();
    break;
```

**Add `HandleBulletContinuation()`:**

```csharp
private bool HandleBulletContinuation()
{
    int    line     = _input.GetCaretLine();
    string lineText = _input.GetLine(line);

    // Only act if this line starts a bullet
    if (!lineText.StartsWith("- ")) return false;

    string body = lineText[2..]; // everything after "- "

    if (string.IsNullOrEmpty(body))
    {
        // Empty bullet — remove the prefix and return to normal text
        _input.SetLine(line, "");
        _input.SetCaretColumn(0);
    }
    else
    {
        // Continue the list on a new line
        int col = _input.GetCaretColumn();
        _input.SetLine(line, lineText[..col]);
        _input.InsertLineAt(line + 1, "- ");
        _input.SetCaretLine(line + 1);
        _input.SetCaretColumn(2);
    }
    return true;
}
```

> The existing `OnInputKey` guard (`if (_acPanel == null || !IsInstanceValid(_acPanel)) return;`) must be relaxed — the bullet handler should fire even when there is no active autocomplete panel. Restructure the early return accordingly.

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
```

Call `RefreshRelatedLinks()` at end of `Load()` and on `_notes.TextChanged`.

**`session_detail_pane.tscn`:** Add `RelatedLinksContainer` VBoxContainer under `CarouselColumn`, after `ImageCarousel`. Remove or reposition `CarouselSpacer`. Wire export.

---

### F4 — NPC Detail Pane: Home Location Field 🔶 Partial

Status and RelationshipTypeId dropdowns are done. Remaining field worth adding:

| Field | Source | Action |
|-------|--------|--------|
| `HomeLocationId` | `Npc` | Add Location `TypeOptionButton` to NPC pane |

`Personality` and `FirstSeenSession` deferred — personality isn't a priority, and first-seen session is better handled as part of the F12 session pane redesign.

---

### F5 — Bulk Type Management Screen

A dedicated screen for managing all per-campaign seeded types in one place — useful for pre-session prep without having to open individual records. The inline `TypeOptionButton` already handles add/delete on the fly; this is purely a convenience screen for bulk editing.

**Recommended approach:** One `CampaignSettingsModal` scene with a `TabContainer`. Each tab is an instance of a shared `TypeEditorPanel` subscene (list + name input + Add/Delete buttons). Each tab wired to a different repository.

**Tables to manage:**

| Tab | Repository | Table |
|-----|------------|-------|
| Species | `SpeciesRepository` | `species` |
| NPC Statuses | `NpcStatusRepository` | `npc_statuses` |
| NPC Relationships | `NpcRelationshipTypeRepository` | `npc_relationship_types` |
| Faction Roles | `NpcFactionRoleRepository` | `npc_faction_roles` |
| Item Types | `ItemTypeRepository` | `item_types` |

> Do not allow deleting a type that any records currently reference — warn the user before deletion.

---

### F6 — Nested Locations in Sidebar ✅

*(See Completed Work Log)*

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

### F12 — Session Detail Pane: Significant Redesign 🔶 In Progress

The current pane has: number label, title, played-on date, wiki notes, image carousel, delete. Most-used pane during active play; confirmed as the biggest active pain point from live session use.

**Confirmed missing (from live session feedback):**
- Abilities panel — quick reference during combat
- Quick-create for entities from within the session (tracked as F18 — see below)
- WikiLink coverage gap — Items and Quests not linked (tracked as F17)
- Bullet point support in notes (tracked as U5)

**Previously planned — still applies:**
- Entity tagging panel — structured fields for which NPCs, locations, factions, and quests appeared
- Inline stub creation — create a new NPC or location without losing place
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

`SettingsRepository` already has a generic `Get(key, default)` / `Set(key, value)` backed by the `app_settings` table. No schema changes needed.

**Implementation — `CampaignDashboard.cs`:**

Write on every navigation:
```csharp
// Inside ShowDetailPane / NavigateToInternal, after loading the pane:
_db.Settings.Set($"last_entity_type_{_campaignId}", entityType);
_db.Settings.Set($"last_entity_id_{_campaignId}", entityId.ToString());
```

Read in `SetCampaign()` after `LoadAll()`:
```csharp
string lastType = _db.Settings.Get($"last_entity_type_{_campaignId}");
string lastIdStr = _db.Settings.Get($"last_entity_id_{_campaignId}");
if (!string.IsNullOrEmpty(lastType) && int.TryParse(lastIdStr, out int lastId))
    ShowDetailPane(lastType, lastId);
```

Per-campaign keys (`last_entity_type_42`) mean each campaign remembers independently.

---

### F15 — NPC–NPC Relationships: Directionality Display

**Design settled.** Full spec below.

#### Schema changes (additive only)

**`character_relationship_types`** — add nullable `reverse_label` column:
```sql
ALTER TABLE character_relationship_types ADD COLUMN reverse_label TEXT;
```
Migration guard pattern (same as all other additive columns):
```csharp
// In CharacterRelationshipTypeRepository.Migrate():
var hasReverse = _conn.CreateCommand();
hasReverse.CommandText = "SELECT COUNT(*) FROM pragma_table_info('character_relationship_types') WHERE name = 'reverse_label'";
if ((long)hasReverse.ExecuteScalar() == 0)
{
    var alter = _conn.CreateCommand();
    alter.CommandText = "ALTER TABLE character_relationship_types ADD COLUMN reverse_label TEXT";
    alter.ExecuteNonQuery();
}
```

**`character_relationships`** — add `is_reversed` boolean:
```sql
ALTER TABLE character_relationships ADD COLUMN is_reversed INTEGER NOT NULL DEFAULT 0;
```
Same migration guard pattern. `DEFAULT 0` means all existing rows are treated as forward — correct, since they were all stored from the `character_id` perspective.

#### Model changes

**`CharacterRelationshipType.cs`:**
```csharp
public string? ReverseLabel { get; set; }  // null = symmetric (use Name for both sides)
```

**`CharacterRelationship.cs`:**
```csharp
public bool IsReversed { get; set; }
```

#### Repository changes

**`CharacterRelationshipTypeRepository`** — add `reverse_label` to all SELECT, INSERT, UPDATE queries. Add to `Map()`:
```csharp
ReverseLabel = reader.IsDBNull(4) ? null : reader.GetString(4),
```

**`NpcRepository.GetRelationships()`** — add `is_reversed` to SELECT and Map():
```csharp
cmd.CommandText = "SELECT character_id, related_character_id, relationship_type_id, is_reversed FROM character_relationships WHERE character_id = @cid OR related_character_id = @cid";
// ...
IsReversed = reader.GetInt32(3) == 1,
```

**`NpcRepository.AddRelationship()`** — add `is_reversed` parameter:
```csharp
public void AddRelationship(int characterId, int relatedCharacterId, int? typeId, bool isReversed)
{
    var cmd = _conn.CreateCommand();
    cmd.CommandText = "INSERT OR IGNORE INTO character_relationships (character_id, related_character_id, relationship_type_id, is_reversed) VALUES (@cid, @rcid, @tid, @rev)";
    cmd.Parameters.AddWithValue("@cid",  characterId);
    cmd.Parameters.AddWithValue("@rcid", relatedCharacterId);
    cmd.Parameters.AddWithValue("@tid",  typeId.HasValue ? typeId.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@rev",  isReversed ? 1 : 0);
    cmd.ExecuteNonQuery();
}
```

#### New component: `RelationshipTypeOptionButton`

A new component that extends or mirrors `TypeOptionButton` but handles the two-column relationship type list. Not a subclass of `TypeOptionButton` (Godot partial classes make inheritance awkward) — a standalone component that shares the same visual conventions.

**Signals:**
```csharp
[Signal] public delegate void TypeSelectedEventHandler(int id, bool isReversed);
[Signal] public delegate void TypeCreatedEventHandler(int id);
[Signal] public delegate void PopupClosedEventHandler();
```

**List rows — two columns per row:**

Each type generates **one row** in the list with two interactive halves:

```
[ Friend of       ] ↔ [ Friend of       ]
[ Master of       ] ↔ [ Slave of        ]
[ Mentor of       ] ↔ [ Student of      ]
[ Acquainted with ] ↔ [ Acquainted with ]
```

- Left button: selects the type with `isReversed = false`
- Right button: selects the type with `isReversed = true`
- If `ReverseLabel` is null, right column displays `Name` (mirrored)
- The `↔` separator is a non-interactive `Label`
- Hover/delete behaviour identical to `TypeOptionButton` — the whole row highlights, `×` appears on hover

**Add form — side by side:**
```
[ Forward label...  ] ↔ [ Reverse label (optional)... ] [ + Add ]
```
Both are `LineEdit`. Submitting with only the forward label filled creates a symmetric type (null `ReverseLabel`). Pressing Enter in either field submits.

**`AutoSelectOnAdd` behaviour:** When a new type is created, emit `TypeSelected(id, isReversed: false)` and close — user picks direction from the list if they want the reverse.

#### Display logic in `NpcDetailPane.LoadRelRows()`

Replace the current `typeNames` dictionary lookup with a helper that accounts for direction:

```csharp
// Build type lookup including reverse labels
var types = new Dictionary<int, CharacterRelationshipType>();
foreach (var t in _db.CharacterRelationshipTypes.GetAll(_npc.CampaignId))
    types[t.Id] = t;

// In the foreach loop:
string label = "";
if (rel.RelationshipTypeId.HasValue && types.TryGetValue(rel.RelationshipTypeId.Value, out var relType))
{
    bool useReverse = rel.IsReversed && !string.IsNullOrEmpty(relType.ReverseLabel);
    label = useReverse ? relType.ReverseLabel : relType.Name;
}
// Row text: "{otherNpcName}, {label} {thisNpcName}" when label non-empty
//           "{otherNpcName}" when label empty
string otherName = npcNames.TryGetValue(capturedOther, out var on) ? on : "Unknown";
var row = new EntityRow
{
    Text = string.IsNullOrEmpty(label) ? otherName : $"{otherName}, {label} {_npc.Name}"
};
```

Note the display format change: previously `"{A}, {type} {B}"` — now always `"{other}, {label} {thisNpc}"` so both sides read naturally. "Iarno, Slave of Sildar" on Sildar's pane; "Sildar, Master of Iarno" on Iarno's pane.

#### `NpcDetailPane` wiring changes

Replace `_relTypeSelect` (`TypeOptionButton`) with `_relTypeSelect` (`RelationshipTypeOptionButton`). Update `OnAddRelPressed()`:

```csharp
private void OnAddRelPressed()
{
    if (!_relNpcSelect.SelectedId.HasValue) return;
    _db.Npcs.AddRelationship(_npc.Id, _relNpcSelect.SelectedId.Value, _relTypeSelect.SelectedId, _relTypeSelect.IsReversed);
    _npc.Relationships = _db.Npcs.GetRelationships(_npc.Id);
    LoadRelRows();
}
```

Add `public bool IsReversed { get; private set; }` to `RelationshipTypeOptionButton`, set when `TypeSelected` fires.

#### Files

| File | Change |
|------|--------|
| `Core/Models/CharacterRelationshipType.cs` | Add `ReverseLabel` property |
| `Core/Models/CharacterRelationship.cs` | Add `IsReversed` property |
| `Core/Repositories/CharacterRelationshipTypeRepository.cs` | Migration guard for `reverse_label`; add to all queries |
| `Core/Repositories/NpcRepository.cs` | Migration guard for `is_reversed`; update `GetRelationships`, `AddRelationship` |
| `Scenes/Components/RelationshipTypeOptionButton/RelationshipTypeOptionButton.cs` | **New** — two-column type picker |
| `Scenes/Components/RelationshipTypeOptionButton/relationship_type_option_button.tscn` | **New** |
| `Scenes/Components/NpcDetailPane/NpcDetailPane.cs` | Swap `_relTypeSelect` type; update `LoadRelRows()`, `OnAddRelPressed()` |
| `Scenes/Components/NpcDetailPane/npc_detail_pane.tscn` | Swap node type for `_relTypeSelect` |

---

### F16 — Call SeedDefaults on Campaign Load

All `SeedDefaults` implementations already use `INSERT ... WHERE NOT EXISTS (... AND name = @name)` — they are fully idempotent and safe to call repeatedly. The only gap is they are currently only called from `DatabaseService` at campaign *creation*, not on subsequent loads.

**Fix — `DatabaseService.cs` or `CampaignDashboard.SetCampaign()`:** Call all `SeedDefaults` on campaign load, not just on creation. Because all the INSERT guards are already in place, this is safe for existing campaigns with real data.

```csharp
// Call in SetCampaign or wherever a campaign is opened:
_db.Species.SeedDefaults(campaignId);
_db.LocationFactionRoles.SeedDefaults(campaignId);
_db.NpcFactionRoles.SeedDefaults(campaignId);
_db.NpcRelationshipTypes.SeedDefaults(campaignId);
_db.NpcStatuses.SeedDefaults(campaignId);
_db.FactionRelationshipTypes.SeedDefaults(campaignId);
_db.CharacterRelationshipTypes.SeedDefaults(campaignId);
_db.ItemTypes.SeedDefaults(campaignId);
_db.QuestStatuses.SeedDefaults(campaignId);
```

This also means any new seeds added to a `Defaults` array in the future will automatically appear in existing campaigns on next open — making F16 the permanent solution, not a one-time patch.

**New seeds to add at the same time:**

| Repository | New entry |
|------------|-----------|
| `CharacterRelationshipTypeRepository` | `("Worships", "Reveres as a deity or divine figure")` |
| `NpcFactionRoleRepository` | `("Worshipped by", "Venerated as a god or divine patron by this faction")` |

These will reach existing campaigns automatically once F16 is wired in.

---

### F17 — WikiNotes: Items and Quests in WikiLink Autocomplete

`GetEntityMatches()` in `WikiNotes.cs` currently queries NPCs, Factions, Locations, and Sessions. Items and Quests are missing, so typing `[[` will not suggest them and their names will not render as links in the viewer.

**Fix — `WikiNotes.cs`, `GetEntityMatches()`:**

```csharp
foreach (var x in _db.Items.GetAll(_campaignId))  Add(x.Name,  "Item");
foreach (var x in _db.Quests.GetAll(_campaignId)) Add(x.Title, "Quest");
```

**Fix — `WikiLinkParser.cs`:** Ensure Items and Quests are included in the reverse-lookup used for rendering `[[links]]` as coloured text. If `WikiLinkParser` builds its own lookup independently of `WikiNotes`, the same two entity types must be added there too.

**Fix — `SessionDetailPane.cs` `BuildReverseLookup()`** (F3): When F3 is implemented, include Items and Quests in the reverse lookup so the related-links panel also surfaces them.

---

### F18 — WikiNotes: Stub Creation via `+EntityType` Syntax

Typing `+NPC`, `+Location`, `+Item`, `+Faction`, or `+Quest` anywhere in WikiNotes (not inside `[[...]]`) opens a lightweight name-prompt modal. Confirming creates a stub record with only a name and inserts `[[Name]]` at the cursor position, replacing the trigger text.

#### Trigger detection

The trigger piggybacks entirely on the existing `[[...]]` detection in `CheckAutocomplete()`. When the query string inside `[[` starts with `+` and matches a known entity type token exactly, the stub modal fires instead of the autocomplete list.

```
[[+NPC]]      → entity type: npc
[[+Location]] → entity type: location
[[+Item]]     → entity type: item
[[+Faction]]  → entity type: faction
[[+Quest]]    → entity type: quest
```

Match is case-insensitive. The trigger fires on the closing `]]` — at that moment `CheckAutocomplete()` sees `query == "+NPC"` (or similar) and opens the modal instead of the list. Partial typing (`[[+NP`) does nothing — the existing autocomplete finds no matches and hides normally.

**Detection logic — extend `CheckAutocomplete()` after the existing `query` extraction:**

```csharp
// Existing code already extracts `query` from inside [[...]]
// Add immediately after:
string stubType = DetectStubTrigger(query);
if (stubType != null)
{
    HideAutocomplete();
    OpenStubModal(stubType, openIdx);
    return;
}
```

```csharp
private static readonly Dictionary<string, string> StubTriggers = new(StringComparer.OrdinalIgnoreCase)
{
    { "+NPC",      "npc"      },
    { "+Location", "location" },
    { "+Item",     "item"     },
    { "+Faction",  "faction"  },
    { "+Quest",    "quest"    },
};

private string DetectStubTrigger(string query)
{
    return StubTriggers.TryGetValue(query, out string entityType) ? entityType : null;
}
```

Note: `CheckAutocomplete()` already returns early if `query.Contains("]]")`, so the trigger only fires while the caret is still inside the open `[[`. The closing `]]` the user types is what produces the final exact match that fires the modal.

#### Stub modal

`OpenStubModal` shows a `PopupPanel` positioned below the caret — same placement logic as the autocomplete panel. It contains:

- A single `LineEdit` pre-focused, placeholder `"Name..."`
- A confirm button (`+ Create`) and implicit Enter key support
- No other fields — stub creation is name-only

```csharp
private void OpenStubModal(string entityType, int triggerStartCol)
{
    HideAutocomplete();

    int    line     = _input.GetCaretLine();
    string lineText = _input.GetLine(line);
    var    caretRect = _input.GetRectAtLineColumn(line, _input.GetCaretColumn());
    var    screenPos = _input.GlobalPosition + caretRect.Position + new Vector2(0, caretRect.Size.Y);

    var popup   = new PopupPanel();
    var vbox    = new VBoxContainer();
    vbox.AddThemeConstantOverride("separation", 6);
    vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

    var nameInput = new LineEdit
    {
        PlaceholderText     = "Name...",
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CaretBlink          = true,
    };
    var addBtn = new Button { Text = "+ Create" };

    Action doCreate = () =>
    {
        string name = nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        CreateStub(entityType, name);

        // The user typed [[+NPC]] — openIdx points to the `[` of `[[`.
        // Replace the whole [[+Token]] with [[Name]].
        string currentLine = _input.GetLine(line);
        int closeIdx = currentLine.IndexOf("]]", openIdx, StringComparison.Ordinal);
        int replaceEnd = closeIdx >= 0 ? closeIdx + 2 : _input.GetCaretColumn();

        string newLine = currentLine[..openIdx] + $"[[{name}]]" + currentLine[replaceEnd..];
        _input.SetLine(line, newLine);
        _input.SetCaretColumn(openIdx + 2 + name.Length + 2);

        popup.Hide();
        _input.GrabFocus();
    };

    addBtn.Pressed          += doCreate;
    nameInput.TextSubmitted += _ => doCreate();

    vbox.AddChild(nameInput);
    vbox.AddChild(addBtn);
    popup.AddChild(vbox);
    popup.PopupHide += () => { popup.QueueFree(); _input.GrabFocus(); };
    AddChild(popup);
    popup.Popup(new Rect2I((int)screenPos.X, (int)screenPos.Y, 220, 70));
    nameInput.CallDeferred(LineEdit.MethodName.GrabFocus);
}
```

#### Stub creation

```csharp
private void CreateStub(string entityType, string name)
{
    if (_db == null) return;
    switch (entityType)
    {
        case "npc":
            _db.Npcs.Add(new Npc { CampaignId = _campaignId, Name = name });
            break;
        case "location":
            _db.Locations.Add(new Location { CampaignId = _campaignId, Name = name });
            break;
        case "item":
            _db.Items.Add(new Item { CampaignId = _campaignId, Name = name });
            break;
        case "faction":
            _db.Factions.Add(new Faction { CampaignId = _campaignId, Name = name });
            break;
        case "quest":
            _db.Quests.Add(new Quest { CampaignId = _campaignId, Name = name });
            break;
    }
}
```

#### Signal for sidebar refresh

When a stub is created, the sidebar needs to know so it can add the new row without requiring a full campaign reload. Add a new signal to `WikiNotes`:

```csharp
[Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);
```

Emit it from `CreateStub()` using the `int` returned by each `Add()` call. The parent pane (e.g. `SessionDetailPane`) connects to this signal and forwards it up the tree to `CampaignDashboard`, which already handles `EntityCreated` signals from other detail panes.

#### Escape handling

If the popup is open and the user presses Escape, the popup closes and the entire `[[+Token]]` is removed from the line — it's a fully typed trigger with no other meaning, so leaving it would just be noise. Focus returns to the `TextEdit` with the caret positioned where the token was.

```csharp
popup.PopupHide += () =>
{
    popup.QueueFree();
    // Remove [[+Token]] if the modal was dismissed without creating
    if (!_stubCreated)
    {
        string currentLine = _input.GetLine(line);
        int closeIdx = currentLine.IndexOf("]]", openIdx, StringComparison.Ordinal);
        int replaceEnd = closeIdx >= 0 ? closeIdx + 2 : openIdx + 2;
        _input.SetLine(line, currentLine[..openIdx] + currentLine[replaceEnd..]);
        _input.SetCaretColumn(openIdx);
    }
    _input.GrabFocus();
};
```

Set `_stubCreated = true` inside `doCreate` before calling `popup.Hide()` so the `PopupHide` handler knows not to clean up.

#### Files

| File | Change |
|------|--------|
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add `DetectStubTrigger()`, `OpenStubModal()`, `CreateStub()`; extend `CheckAutocomplete()`; add `EntityCreated` signal |
| `Scenes/Components/SessionDetailPane/SessionDetailPane.cs` | Connect `_notes.EntityCreated` → forward to `CampaignDashboard` |
| All other panes using `WikiNotes` | Connect `EntityCreated` the same way if applicable |

---

## Implementation Order

### Short-term
1. **U4 — TypeOptionButton auto-select** — one-liner per call site, no design needed.
2. **F14 — Remember last opened entity** — two `Settings.Get/Set` calls; no schema changes.
3. **F16 — Call SeedDefaults on campaign load** — trivial wiring; ships the God/Worship seeds too.
4. **F15 — NPC relationship directionality** — new component + two additive migrations; design settled. Do before more relationships are entered.
5. **F17 — Items + Quests in WikiLink autocomplete** — two extra queries in `GetEntityMatches` + `WikiLinkParser`.
6. **F18 — WikiNotes stub creation** — self-contained `WikiNotes.cs` addition; design is settled.
7. **F3 — Session related-links panel** — no schema changes.

### Medium-term
8. **F4 — NPC pane: Home Location field** — small addition, finish the partial work.
9. **F1 — Standardize image save location** — prerequisite for F10.
10. **F5 — Bulk type management screen** — convenience, not blocking anything.
11. **F8 — Players section** — basic party overview.
12. **F2 — Back/forward navigation** — largest navigation change; do after simpler fixes are stable.

### Planned / Larger scope
12. **F9 — PC abilities / class features** — new table, greenfield.
13. **F12 + F13 — Session redesign + Tab system** — plan together.
14. **F10 — Image export/import** — requires F1.
15. **F7 — Campaign cover image** — one enum entry; trivial but low priority.
16. **U5 — Bullet point continuation** — nice to have; defer until session redesign work.

### Blocked / No decision
- **F11 — NPC–Location relationship** — revisit later; design TBD.

---

## File Change Summary

| File | Change | Task |
|------|--------|------|
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Magic-byte format detection + raw byte loading; OS drop via `Window.FilesDropped` | B1, B2 |
| `Scenes/Components/ImageLightbox/ImageLightbox.cs` | Magic-byte format detection + raw byte loading | B1 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Fix `_renderer.GuiInput` handler; add `RestoreScroll()` | U1 |
| `Scenes/Components/EntityRow/EntityRow.cs` | Add `MouseDefaultCursorShape = CursorShape.PointingHand` in `_Ready()` | U2 |
| `theme.tres` | **New** — project-wide Godot theme: TextEdit/LineEdit styles, NavBar, hover states, popup bg | U3 |
| `project.godot` | `[gui] theme/custom`, `[rendering] environment/defaults/default_clear_color` | U3 |
| `Scenes/Components/NavBar/nav_bar.tscn` | Inline StyleBoxFlat panel override — background `#0f172a` | U3 |
| `Scenes/Components/EntityRow/EntityRow.cs` | Hover boxes read from theme with hardcoded fallback | U3 |
| `Scenes/Components/TypeOptionButton/TypeOptionButton.cs` | Hover boxes from theme; popup bg from theme; `CaretBlink = true` on dynamic LineEdits | U3 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Background `#1e293b`, border `#334155` to match app palette | U3 |
| `Scenes/Components/*/[detail pane].tscn` (×5) | `margin_top = 8` restored on `Margin` node | U3 |
| `Scenes/Components/*/[detail pane].tscn` (all) | `caret_blink = true` on all LineEdit and TextEdit nodes | U3 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | `caret_blink = true` on SearchInput | U3 |
| `Scenes/Modals/NewCampaignModal/add_campaign_modal.tscn` | `caret_blink = true` on NameLineEdit and DescriptionTextEdit | U3 |
| `Scenes/App.cs` | `_Input` override — release focus when clicking outside active control | U3 |
| `NpcDetailPane.cs`, `ItemDetailPane.cs`, `QuestDetailPane.cs`, `FactionDetailPane.cs`, `LocationDetailPane.cs` | Add `AutoSelectOnAdd = true` before each type dropdown `Setup()` call | U4 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add `HandleBulletContinuation()`; extend `OnInputKey` Enter handler; relax early-return guard | U5 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Copy image to managed path on add; store relative path | F1 |
| `Scenes/Components/TabHistory.cs` | **New** — `TabHistory` helper class | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Add history, back/forward buttons, `NavigateToInternal`, `_UnhandledInput`, `RefreshNavButtons()` | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | Add `←` / `→` buttons; wire exports | F2 |
| `Scenes/Components/SessionDetailPane/SessionDetailPane.cs` | Add `ParseSessionLinks()`, `BuildReverseLookup()`, `RefreshRelatedLinks()`; update `_notes.TextChanged` | F3 |
| `Scenes/Components/SessionDetailPane/session_detail_pane.tscn` | Add `RelatedLinksContainer` under `CarouselColumn`; remove/reposition `CarouselSpacer` | F3 |
| `Scenes/Components/NpcDetailPane/NpcDetailPane.cs` + `.tscn` | Add HomeLocationId, FirstSeenSession, verify Personality | F4 |
| `Core/Repositories/*RelationshipTypeRepository.cs` et al. | Add check-and-insert on campaign load for new seeds | F16 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add Items + Quests to `GetEntityMatches()` | F17 |
| `Core/WikiLinkParser.cs` | Add Items + Quests to reverse-lookup for link rendering | F17 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add `DetectStubTrigger()`, `OpenStubModal()`, `CreateStub()`; extend `CheckAutocomplete()`; add `EntityCreated` signal | F18 |
| `Scenes/Components/SessionDetailPane/SessionDetailPane.cs` + all other WikiNotes-bearing panes | Connect `_notes.EntityCreated` → forward to `CampaignDashboard` | F18 |

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

### Visual / Theme polish (2026-03-20)
- ✅ U3 — Project-wide Godot theme (`theme.tres`): TextEdit + LineEdit steel-blue bg (`#334155`), violet focus ring (`#6d28d9`), rounded corners (r=5), transparent resting border; warm light text; `Label/colors/font_color` set globally.
- ✅ ThemeManager — `Core/ThemeManager.cs` autoload; HSV hue-shift approach: all themes are Slate's dark profile rotated to any hue (0–359°). Free hue slider + Dark/Light toggle + 4-stop saturation selector (Greyscale · Muted · Default · Vivid) in Settings → Appearance popup. Persisted via `SettingsRepository` (`app_settings` table, keys: `theme_hue`, `theme_dark`, `theme_sat`). NavBar bg and ImageCarousel bg (Component/700) update live via `ThemeChanged` signal. Special-case: hue=215 + dark + Default sat → exact Slate Tailwind hex values.
- ✅ App background `#1e293b` via `rendering/environment/defaults/default_clear_color` in `project.godot` (no Panel nodes in scene tree — clear colour is the correct mechanism).
- ✅ NavBar background `#0f172a` via inline StyleBoxFlat override in `nav_bar.tscn`.
- ✅ EntityRow + TypeOptionButton hover states read from `DndBuilder/styles/row_hover` + `delete_hover` in theme; fallback to hardcoded colours if theme lookup fails (prevents silent null-stylebox override bug).
- ✅ TypeOptionButton popup background from `PopupPanel/styles/panel` in theme.
- ✅ ImageCarousel background changed to `#1e293b` (app background), border to `#334155`; border width set to 0 (user preference).
- ✅ `margin_top = 8` restored on `Margin` node in all five detail panes that were missing it (npc, faction, location, session, item).
- ✅ `caret_blink = true` added to every LineEdit and TextEdit node across all detail panes, the dashboard search field, the new-campaign modal, and dynamically created TypeOptionButton search/add inputs.
- ✅ Global unfocus on blank click — `App._Input` override releases focus when a left-click lands outside the currently focused control. (`_UnhandledInput` was insufficient because detail panes are `ScrollContainer` roots which absorb mouse events before they reach unhandled input.)

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
- ✅ NPC–NPC relationships — `character_relationships` table, full CRUD, `NpcDetailPane` panel with type/NPC pickers, add/remove/navigate. Both directions surface on both NPC panes via `OR related_character_id = @cid` query. Directionality display is a separate open design question (F15).

---

*Generated March 2026 · TTRPG Companion App · Master Action Plan · v1.1*
