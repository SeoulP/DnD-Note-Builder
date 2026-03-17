# TTRPG Companion App — UI Action Plan
*March 2026 · Post-Model Refactor · Pre-Alpha*

---

## Overview

This document captures all UI work required following the model refactor in `Model_Action_Plan.md`. The data layer is complete. This plan covers broken compile fixes first, then NPC detail improvements, then new screens.

> **Guiding principle:** Fix breakage before building new. All compile errors must be resolved before any new UI work begins.

---

## Task Summary

| # | Task | Type | Scope |
|---|------|------|-------|
| 1 | Fix `FactionDetailPane` — remove Headquarters | Compile fix | `FactionDetailPane.cs` + `.tscn` |
| 2 | Fix `LocationDetailPane` — `FactionIds` → `Factions` | Compile fix | `LocationDetailPane.cs` |
| 3 | Fix `NpcDetailPane` — replace enum dropdowns with DB-loaded dropdowns | Compile fix + enhancement | `NpcDetailPane.cs` |
| ★ 4 | NPC detail pane — full field audit post-refactor | Enhancement | `NpcDetailPane.cs` + `.tscn` |
| ★ 5 | Campaign Settings screen — manage seeded types | New screen | New scene(s) |
| ★ 6 | Item list + detail pane | New screen | New scenes |

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

## Deferred (Not This Pass)

| Item | Reason |
|------|--------|
| Location → Faction role picker | Requires Task 5 (role types UI) to exist first |
| Character inventory (CharacterItem) | Requires Item UI (Task 6) to exist first |
| Location item list (LocationItem) | Same |
| PlayerCharacter UI | Phase 2 — character sheet module |
| SystemItem / DnD5eItemMechanics UI | Phase 2 |

---

*Generated March 2026 · TTRPG Companion App · Post-Model-Refactor UI Plan · v1.0*