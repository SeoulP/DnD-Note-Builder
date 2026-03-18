# TTRPG Companion App — UI Action Plan
*March 2026 · Post-Model Refactor · Pre-Alpha*

---

## Overview

This document captures all UI work required following the model refactor in `Model_Action_Plan.md`. The data layer is complete. This plan covers broken compile fixes first, then NPC detail improvements, then new screens.

> **Guiding principle:** Fix breakage before building new. All compile errors must be resolved before any new UI work begins.

---

## Task Summary

| # | Task | Type | Scope | Status |
|---|------|------|-------|--------|
| 1 | Fix `FactionDetailPane` — remove Headquarters | Compile fix | `FactionDetailPane.cs` + `.tscn` | ✅ Done |
| 2 | Fix `LocationDetailPane` — `FactionIds` → `Factions` | Compile fix | `LocationDetailPane.cs` | ✅ Done |
| 3 | Fix `NpcDetailPane` — replace enum dropdowns with DB-loaded dropdowns | Compile fix + enhancement | `NpcDetailPane.cs` | ✅ Done |
| ★ 4 | NPC detail pane — full field audit post-refactor | Enhancement | `NpcDetailPane.cs` + `.tscn` | 🔶 Partial — Status/Relationship done; Personality, HomeLocationId, FirstSeenSession still missing |
| ★ 5 | Campaign Settings screen — manage seeded types | New screen | New scene(s) | ⬜ Todo |
| ★ 6 | Item list + detail pane | New screen | New scenes | ✅ Done |
| ★ 7 | Entity image carousel system — multi-image, all panes | New components + data layer + enhancement | All 5 detail panes | ⬜ Todo |

*★ = new work beyond compile fixes*

---

## Task 1 — Fix `FactionDetailPane`: Remove Headquarters

### What & Why

`Faction.Headquarters` was deleted from the model. `FactionDetailPane` still reads and writes it, causing a compile error.

### Changes

**`FactionDetailPane.cs`:**
- Remove the `[Export] private LineEdit _headquartersInput;` field (or whichever control is wired to HQ).
- Remove the two lines that read/write `faction.Headquarters`.

**`FactionDetailPane.tscn`:**
- Delete the Headquarters label and LineEdit nodes.
- Remove the export wire for `_headquartersInput`.

> **Going forward:** A faction's location is expressed through `location_factions` join records (Location → Faction with a role). No plain text HQ field anywhere.

---

## Task 2 — Fix `LocationDetailPane`: `FactionIds` → `Factions`

### What & Why

`Location.FactionIds` (`List<int>`) was replaced by `Location.Factions` (`List<LocationFaction>`). Line 140 of `LocationDetailPane.cs` builds a `HashSet<int>` from `FactionIds`, which no longer exists.

### Change

**`LocationDetailPane.cs` line ~140:**

Replace:
```csharp
var memberSet = new HashSet<int>(_location.FactionIds);
```

With:
```csharp
var memberSet = new HashSet<int>(_location.Factions.Select(f => f.FactionId));
```

Add `using System.Linq;` at the top if not already present.

> **Note:** The `role_id` on each `LocationFaction` is not surfaced in the UI at this stage — that is deferred to Task 5 (Campaign Settings). The member set fix is purely to restore existing faction-checkbox behaviour.

---

## Task 3 — Fix `NpcDetailPane`: Replace Enum Dropdowns

### What & Why

`NpcDetailPane` currently populates Status and Relationship dropdowns from `System.Enum.GetNames(...)` and casts the selected index back to the enum. Both enums are gone. Status and Relationship are now nullable FK ints pointing to per-campaign seeded tables.

### Changes

**`NpcDetailPane.cs`:**

1. Remove:
   ```csharp
   foreach (var s in System.Enum.GetNames(typeof(NpcStatus)))       _statusInput.AddItem(s);
   foreach (var r in System.Enum.GetNames(typeof(NpcRelationship))) _relationshipInput.AddItem(r);
   ```

2. Add fields to hold the loaded options:
   ```csharp
   private List<NpcStatus>          _statuses      = new();
   private List<NpcRelationshipType> _relationships = new();
   ```

3. Load from DB after the campaign is known (e.g. in `LoadNpc` or wherever `_campaignId` is set):
   ```csharp
   _statuses      = _db.NpcStatuses.GetAll(_campaignId);
   _relationships = _db.NpcRelationshipTypes.GetAll(_campaignId);

   _statusInput.Clear();
   _statusInput.AddItem("— None —");  // index 0 = null
   foreach (var s in _statuses) _statusInput.AddItem(s.Name);

   _relationshipInput.Clear();
   _relationshipInput.AddItem("— None —");
   foreach (var r in _relationships) _relationshipInput.AddItem(r.Name);
   ```

4. Replace save logic:
   ```csharp
   // Old:
   _npc.Status       = (NpcStatus)_statusInput.Selected;
   _npc.Relationship = (NpcRelationship)_relationshipInput.Selected;

   // New (index 0 = None = null; indices 1+ map to the list):
   _npc.StatusId           = _statusInput.Selected      == 0 ? null : _statuses[_statusInput.Selected - 1].Id;
   _npc.RelationshipTypeId = _relationshipInput.Selected == 0 ? null : _relationships[_relationshipInput.Selected - 1].Id;
   ```

5. Replace load logic (when populating the form from an existing NPC):
   ```csharp
   _statusInput.Select(_npc.StatusId.HasValue
       ? _statuses.FindIndex(s => s.Id == _npc.StatusId.Value) + 1
       : 0);
   _relationshipInput.Select(_npc.RelationshipTypeId.HasValue
       ? _relationships.FindIndex(r => r.Id == _npc.RelationshipTypeId.Value) + 1
       : 0);
   ```

> **UI note:** `FindIndex` returns -1 if the id is not found (e.g. a type was deleted). Adding 1 gives 0, which selects "— None —" — a safe fallback.

---

## Task 4 — NPC Detail Pane: Full Field Audit

### What & Why

After the model refactor the NPC detail pane's field set may be stale. This task audits and updates the pane to match the current `Npc : Character` model.

### Field Checklist

| Field | Source | Pane Has It? | Action |
|-------|--------|-------------|--------|
| Name | Character | Yes | Keep |
| PortraitPath | Character | Yes (path input) | Keep |
| Gender | Character | Yes | Keep |
| Occupation | Character | Yes | Keep |
| Description | Character | Yes | Keep |
| Personality | Character | Yes | Keep |
| Notes | Character | Yes | Keep |
| SpeciesId | Character | Yes (dropdown) | Keep |
| FactionIds | Character | Yes (checkboxes) | Keep — still `List<int>` on Character |
| HomeLocationId | Npc | Check | Add if missing |
| FirstSeenSession | Npc | Check | Add if missing |
| RelationshipTypeId | Npc | Needs fix | Task 3 above |
| StatusId | Npc | Needs fix | Task 3 above |

> After Task 3 is done, walk through the `.tscn` and `.cs` together to verify every exported node is wired and every model field is read/written.

---

## Task 5 — Campaign Settings: Manage Seeded Types

### What & Why

Five per-campaign tables are seeded on campaign creation but need UI for users to add, rename, and delete entries. All five screens are nearly identical in structure — a list with add/delete, and a name + description field.

### Tables to Manage

| Screen Tab | Repository | Table |
|------------|------------|-------|
| Species | `SpeciesRepository` | `species` |
| NPC Statuses | `NpcStatusRepository` | `npc_statuses` |
| NPC Relationships | `NpcRelationshipTypeRepository` | `npc_relationship_types` |
| Faction Roles | `LocationFactionRoleRepository` | `location_faction_roles` |
| Item Types | `ItemTypeRepository` | `item_types` |

### Recommended Approach

One `CampaignSettingsModal` scene with a `TabContainer`. Each tab is an instance of a shared `TypeEditorPanel` subscene:

```
TypeEditorPanel
  ├── ItemList (VBoxContainer of rows)
  ├── NameInput (LineEdit)
  ├── DescriptionInput (TextEdit)
  ├── AddButton
  └── DeleteButton (enabled when a row is selected)
```

Each tab is wired to a different repository via a shared interface or by passing in lambdas/delegates for GetAll / Add / Delete.

> **Constraint:** Do not allow deleting a type if any records reference it — SQLite `ON DELETE SET NULL` handles the FK gracefully, but a warning prompt is good UX.

---

## Task 6 — Item List + Detail Pane

### What & Why

The Item model and `ItemRepository` are complete. Users need a way to see, add, edit, and delete items within a campaign.

### Scenes Required

| Scene | Purpose |
|-------|---------|
| `ItemListPane` | Scrollable list of items for the active campaign; Add button |
| `ItemDetailPane` | Name, Description, Notes, IsUnique toggle, TypeId dropdown |
| `NewItemModal` (optional) | Minimal add flow if a full detail pane is too heavy for first entry |

### Field Map

| UI Control | Model Field | Notes |
|------------|-------------|-------|
| LineEdit | `Name` | Required |
| TextEdit | `Description` | |
| TextEdit | `Notes` | DM-only secrets |
| CheckBox | `IsUnique` | "Named / one-of-a-kind item" |
| OptionButton | `TypeId` | Loaded from `ItemTypeRepository.GetAll(campaignId)` |

### Sidebar Integration

Items will need a sidebar section. Follow the same pattern as the Locations or NPCs section. Default sort position: after Factions.

---

## Task 7 — Entity Image Carousel System

### What & Why

All entities (NPC, Location, Item, Faction, Session) should support **multiple images** — character portraits, faction banners, location art, session screenshots, etc. A single `PortraitPath` string is insufficient. The UI should be a self-contained carousel: left/right arrows to cycle through images, a `+` button to add more, and click-to-open lightbox for pan/zoom.

> **Note:** `Character.PortraitPath` already exists on the model and in the DB as a single string. It will be **migrated** into the new `entity_images` table on first load and the column left in place (ignored going forward) to avoid destructive schema changes.

---

### Data Layer

#### New enum: `EntityType`

```csharp
// Core/Models/EntityType.cs
namespace DndBuilder.Core.Models
{
    public enum EntityType { Npc, Location, Item, Faction, Session }
}
```

Stored in the DB as its integer value (`(int)EntityType.Npc`, etc.).

#### New model: `EntityImage`

```csharp
// Core/Models/EntityImage.cs
namespace DndBuilder.Core.Models
{
    public class EntityImage
    {
        public int        Id         { get; set; }
        public EntityType EntityType { get; set; }
        public int        EntityId   { get; set; }
        public string     Path       { get; set; } = "";
        public int        SortOrder  { get; set; } = 0;
    }
}
```

#### New repository: `EntityImageRepository`

```csharp
// Core/Repositories/EntityImageRepository.cs
```

Table DDL (inside `Migrate()`):

```sql
CREATE TABLE IF NOT EXISTS entity_images (
    id          INTEGER PRIMARY KEY,
    entity_type TEXT    NOT NULL,
    entity_id   INTEGER NOT NULL,
    path        TEXT    NOT NULL DEFAULT '',
    sort_order  INTEGER NOT NULL DEFAULT 0
)
```

Methods needed:
- `List<EntityImage> GetAll(EntityType entityType, int entityId)` — ordered by `sort_order ASC`
- `int Add(EntityImage img)` — inserts, returns new id
- `void Delete(int id)`
- `void Reorder(int id, int newSortOrder)` — for drag-to-reorder (deferred; nav arrows are enough for now)

Register in `DatabaseService.cs`:
- Add `public EntityImageRepository EntityImages { get; private set; }`
- Instantiate and call `.Migrate()` alongside other repos

#### Migration of existing `Character.PortraitPath`

In `NpcRepository` (or `DatabaseService` startup), after `entity_images` is migrated: for any NPC whose `portrait_path` is non-empty and has no rows in `entity_images`, insert one row with `sort_order = 0`. Do not alter or drop the `portrait_path` column.

---

### Scenes / Components Required

| Scene | Purpose |
|-------|---------|
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` + `.tscn` | Self-contained carousel component. Shows one image at a time; prev/next arrows; `+` button to add via file dialog or drag-and-drop; emits `ImagesChanged`. |
| `Scenes/Components/ImageLightbox/ImageLightbox.cs` + `.tscn` | Full-screen overlay for pan + zoom. Instantiated by `ImageCarousel` on image click; freed on close. |

---

### ImageCarousel Layout

```
ImageCarousel (PanelContainer, fixed size e.g. 220×220, rounded corners)
  ├── TextureRect (_image)          — fills area; stretch = KeepAspectCovered; click → open lightbox
  ├── Label (_emptyHint)            — "Drop image here" centered; visible only when image list is empty
  ├── Button (_prevButton)          — "‹" left-center; hidden when ≤ 1 image
  ├── Button (_nextButton)          — "›" right-center; hidden when ≤ 1 image
  ├── Button (_addButton)           — "+" bottom-center; always visible
  └── Label (_counter)              — "2 / 5" bottom-right; hidden when ≤ 1 image
```

Visual matches sketch: rounded border, arrows on left/right vertical midpoints, `+` at bottom center.

**Behaviour:**
- `_prevButton` / `_nextButton`: cycle `_currentIndex` and call `ShowCurrent()`
- `_addButton`: opens `FileDialog` filtered to `.png .jpg .jpeg .webp`; on confirm inserts a new `EntityImage` row via repo, appends to internal list, advances to last index
- **Drag-and-drop:** override `_CanDropData` / `_DropData`; accept file paths with matching extensions
- **Missing file:** if `Path` is set but `File.Exists(path)` is false, show a broken-image placeholder icon instead of crashing
- **File loading:** `Image.LoadFromFile(path)` → `ImageTexture.CreateFromImage(img)`

**Public API:**

```csharp
// Called by the parent detail pane during Load()
public void Setup(EntityType entityType, int entityId, DatabaseService db)

// Reloads from DB and refreshes display — call after external changes if needed
public void Refresh()
```

No `PathChanged` signal needed — the carousel writes directly to the repo on add/delete.

---

### ImageLightbox Layout

```
ImageLightbox (CanvasLayer, full-screen)
  ├── ColorRect (_backdrop)         — semi-transparent black (alpha ~0.75); click closes
  ├── TextureRect (_imageDisplay)   — centered; pan with left-drag; scroll wheel zooms (min 0.25×, max 4×)
  └── Button (_closeButton)         — "×" top-right corner
```

- **Pan:** left-mouse drag → delta applied to `_imageDisplay.Position`
- **Zoom:** scroll wheel; scale pivot at mouse position; clamp 0.25×–4×
- **Close:** click backdrop, click ×, or press Escape
- **Instantiation:** `ImageCarousel` instantiates it via `GD.Load<PackedScene>(...)`, passes the current `ImageTexture`, and adds it to the scene tree; lightbox frees itself on close

---

### Integration Points

| Detail Pane | Entity Type String | Placement |
|-------------|-------------------|-----------|
| `NpcDetailPane` | `"npc"` | Top-right of form, beside Species/Gender/Occupation block |
| `LocationDetailPane` | `"location"` | Top-right of form |
| `ItemDetailPane` | `"item"` | Top-right of form |
| `FactionDetailPane` | `"faction"` | Top-right of form |
| `SessionDetailPane` | `"session"` | Top-right of form |

Each pane:
1. Adds `[Export] private ImageCarousel _imageCarousel;`
2. In `Load()`: calls `_imageCarousel.Setup("entitytype", entity.Id, _db)`
3. No signal wiring needed — carousel is self-contained

---

### Implementation Checklist

- [x] `Core/Models/EntityType.cs` — new enum
- [x] `Core/Models/EntityImage.cs` — new model
- [x] `Core/Repositories/EntityImageRepository.cs` — new repo with Migrate / GetAll / Add / Delete / MigrateLegacyPortrait
- [x] `DatabaseService.cs` — register `EntityImages` repo, call Migrate, call MigrateLegacyPortraits
- [x] `Scenes/Components/ImageCarousel/ImageCarousel.cs` + `.tscn`
- [x] `Scenes/Components/ImageLightbox/ImageLightbox.cs` + `.tscn`
- [x] Wire `ImageCarousel` into `NpcDetailPane` (.cs) — **`.tscn` still needs carousel node added**
- [x] Wire `ImageCarousel` into `LocationDetailPane` (.cs) — **`.tscn` still needs carousel node added**
- [x] Wire `ImageCarousel` into `ItemDetailPane` (.cs) — **`.tscn` still needs carousel node added**
- [x] Wire `ImageCarousel` into `FactionDetailPane` (.cs) — **`.tscn` still needs carousel node added**
- [x] Wire `ImageCarousel` into `SessionDetailPane` (.cs) — **`.tscn` still needs carousel node added**

---

## Deferred (Not This Pass)

| Item | Reason |
|------|--------|
| Location → Faction role picker | Requires Task 5 (role types UI) to exist first |
| Character inventory (CharacterItem) | Requires Item UI (Task 6) to exist first |
| Location item list (LocationItem) | Same |
| PlayerCharacter UI | Phase 2 — character sheet module |
| SystemItem / DnD5eItemMechanics UI | Phase 2 |

---

## Future TODOs

- **Remember last opened file per campaign.** When reopening a campaign, restore the last viewed entity (type + id) so the detail pane is shown immediately rather than landing on a bare sidebar.

---

*Generated March 2026 · TTRPG Companion App · Post-Model-Refactor UI Plan · v1.0*