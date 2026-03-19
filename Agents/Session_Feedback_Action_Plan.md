# TTRPG Companion App — Session Feedback Action Plan
*March 2026 · Post-First-Use-Session · v1.0*

---

## Overview

This document captures all feedback, bugs, and design decisions arising from the first real use-case session with the app. Data is now being entered in earnest — the guiding constraint from this point forward is that the schema is frozen for tables with existing data. All changes must be additive.

> **Guiding rule:** No destructive migrations. Column renames, drops, or type changes on tables that contain real data are prohibited. All schema work must use additive patterns: new columns with defaults, new join tables, or new tables.

---

## Task Summary

| # | Task | Category | Priority | Status | File(s) |
|---|------|----------|----------|--------|---------|
| 1 | Space bar triggers image importer from lightbox | Bug | High | ✅ Done | `ImageCarousel.cs` |
| 2 | Drag-and-drop image upload not working | Bug | High | ⬜ Todo | `project.godot` |
| 3 | Session sidebar — show title only, no index prefix | UX | High | ✅ Done | `CampaignDashboard.cs`, `SessionDetailPane.cs` |
| 4 | Wasted space left/right of app | UX | High | ✅ Done | `App.cs` |
| 5 | Import / Export database | Feature | High | ✅ Done | `NavBar.cs`, `ImportExportModal.cs`, `ImportExportService.cs`, `ExportPackage.cs` |
| 6 | All text areas auto-expand in edit mode | UX | Medium | ✅ Done | `WikiNotes.cs` |
| 7 | Entity name fields select-all on focus | UX | Medium | ✅ Done | All detail panes |
| 8 | Lightbox cursor shows magnifier on hover | UX | Medium | ✅ Done | `ImageLightbox.cs` |
| 9 | Image picker — OS-native file dialog | UX | Medium | ✅ Done | `ImageCarousel.cs`, `NavBar.cs` |
| 10 | Navbar — define purpose and build out | UX | Medium | ✅ Done | `NavBar.cs` |
| 11 | Add "Acquainted with" NPC relationship seed | Schema | Medium | ✅ Done | `CharacterRelationshipTypeRepository.cs` |
| 12 | Background colour — investigate Godot Themes | UX | Low | ⬜ Todo | Global (`.tres`) |
| 13 | Nested locations in sidebar | Design | Low | ⬜ Todo | `CampaignDashboard.cs` |
| 14 | Three-column layout — sidebar / detail / wiki | Design | Low | ✅ Done | `CampaignDashboard.cs` |
| 15 | Session detail pane — significant development needed | Design | Planned | ⬜ Todo | `SessionDetailPane.cs` |
| 16 | Campaign cover image | Feature | Planned | ⬜ Todo | `EntityType.cs`, CampaignCard |
| 17 | Players section — party overview | Feature | Planned | ⬜ Todo | `CampaignDashboard.cs` |
| 18 | PC abilities / class features | Feature | Planned | ⬜ Todo | Greenfield |
| 19 | Quests entity + Quest History sub-feature | Feature | Planned | ✅ Done | `Quest.cs`, `QuestHistory.cs`, `QuestStatus.cs`, `QuestRepository.cs`, `QuestDetailPane`, `CampaignDashboard` |
| 20 | NPC–NPC relationships (design open) | Feature | Planned | ✅ Done | `NpcDetailPane.cs` |
| 21 | NPC–Location relationship (design open) | Feature | Planned | ⬜ Todo | `Npc.cs` |
| 22 | History section (concept only) | Parked | TBD | ⬜ Parked | — |
| 23 | Tab system for the detail pane | Design | Planned | ⬜ Todo | `CampaignDashboard.cs` (new `TabBar` component) |
| 24 | Notes consistency — Notes last in all detail panes | UX | High | ✅ Done | `location_detail_pane.tscn` |
| 25 | Detail pane footer — breathing room at window bottom | UX | High | ✅ Done | `CampaignDashboard.cs` |
| 26 | New sessions always append to end of sidebar list | Bug | High | ✅ Done | `CampaignDashboard.cs`, `ImportExportService.cs` |
| 27 | Settings menu tooltips | UX | Medium | ✅ Done | `NavBar.cs` |
| 28 | Standardize image save location to `imgs/` folder | Feature | High | ⬜ Todo | `ImageCarousel.cs`, `EntityImageRepository.cs` |
| 29 | Image export/import in `.dndx` packages | Feature | Medium | ⬜ Todo | `ImportExportService.cs`, `ExportPackage.cs` |

---

## Bugs

### Task 1 — Space bar opens image importer from lightbox ✅ Done

The last focused UI element retains focus and receives the Space key even after the lightbox opens. This was observed because the Add (+) button was the last element activated before opening the lightbox.

**Fix**

In `ImageCarousel.OpenLightbox()`, call `GetViewport().GuiReleaseFocus()` before adding the lightbox to the scene tree. This releases focus from all UI elements globally — not just the add button — so no stale key events bleed through.

```
File: Scenes/Components/ImageCarousel/ImageCarousel.cs
Method: OpenLightbox()
```

---

### Task 2 — Drag-and-drop image upload not working

`_CanDropData` and `_DropData` are correctly implemented in `ImageCarousel.cs` and the logic looks right. This is most likely a Godot project setting rather than a code bug.

**Fix**

1. Check `display/window/drag_and_drop/enable` in `project.godot` — this flag must be on for drag-and-drop to work on desktop.
2. If already enabled, check whether a parent node in the scene tree is consuming the input event before it reaches the carousel.

---

## UX Polish

### Task 3 — Session sidebar: title only, no index prefix ✅ Done

The session sidebar buttons currently display a hardcoded format of `#001 – Title`. The numbering was a false assumption — users who do not name sessions should not see auto-generated indexes, and users who do name sessions are responsible for their own naming conventions.

**Changes**

In `CampaignDashboard.LoadSessions()`, change the button text:

```
From: $"#{session.Number:D3} – {session.Title}"
To:   string.IsNullOrEmpty(session.Title) ? "Untitled Session" : session.Title
```

Also update the `NameChanged` signal emit in `SessionDetailPane` to use the same format — it currently emits the prefixed string which would put the prefix back in the sidebar on rename.

---

### Task 4 — Wasted space left and right of the app ✅ Done

The root cause is found. `MarginBox` in `app.tscn` has hardcoded margins of 200px left, 200px right, 50px top, and 50px bottom. These were presumably scaffold values that were never removed.

**Fix (implemented)**

`app.tscn` margins set to 0. `App.cs` now toggles margins via `SetMargins()` when switching panels: 200/200/50/50 for the campaign list, 0/0/0/0 for the dashboard. This preserves the breathing room on the list screen while using full width in the dashboard. A 24px bottom gap was also added to `_detailPanel` in `CampaignDashboard.ApplySidebarWidth()` so content doesn't butt against the window edge.

---

### Task 6 — All text areas should auto-expand in edit mode ✅ Done

The `WikiNotes` component uses a `TextEdit` for input. It currently has a fixed minimum height, which forces the user to scroll inside the field — especially painful in the Session detail pane.

**Fix (implemented)**

`WikiNotes` uses a `UpdateInputHeight()` method called on `TextChanged` and also when the renderer is clicked to activate edit mode (preventing the collapse-on-click bug). Uses `GetLineWrapCount()` per line for accurate wrap-aware height, with a 120px minimum. After resizing, `CallDeferred(ScrollToCaretInParent)` scrolls the ancestor `ScrollContainer` to keep the caret in view.

---

### Task 7 — Entity name fields should select-all on focus

Clicking on an entity name field currently requires a triple-click to select all text. The expected behaviour is that focusing the field immediately selects all text so the user can type a new name without extra clicks.

**Fix**

On every `LineEdit` used as an entity name field, connect `focus_entered` to `SelectAll()`. Affects all five detail panes — worth a shared helper or base class method.

```csharp
_titleInput.FocusEntered += () => _titleInput.SelectAll();
```

---

### Task 8 — Lightbox cursor should show magnifier on hover ✅ Done

There is no visual affordance on the image carousel indicating that the image is clickable and will open a zoomed lightbox.

**Fix (implemented)**

- `ImageCarousel._image`: `MouseDefaultCursorShape = CursorShape.PointingHand` — signals the image is clickable.
- `ImageLightbox._imageDisplay`: `MouseDefaultCursorShape = CursorShape.Move` — signals pan is available. A magnifier cursor was considered but Move is equally valid for a pan/zoom context.

---

### Task 9 — Image picker: use OS-native file dialog

Godot's `FileDialog` with `Access = Filesystem` is Godot's own dialog UI, not the operating system's file picker. It does not show image thumbnails and looks out of place on Windows and macOS.

**Fix**

Use `DisplayServer.FileDialogShow()` which was added in Godot 4.3 and calls the OS dialog natively — no plugin required. It returns the selected path via a callback.

Check the project's Godot version before building toward this. If on an earlier version, a thumbnail preview panel inside the existing `FileDialog` is an acceptable interim solution.

---

### Task 10 — Navbar: define purpose and build out

`NavBar.cs` currently exists but is essentially empty. It renders as a bar at the top of the screen with no content.

**Suggested content**

- Left: current campaign name
- Left: Switch Campaign button
- Right: Import / Export buttons (see Task 5)
- Right: Global search trigger (future)

---

### Task 12 — Background colour: investigate Godot Themes

Colours are currently hardcoded per-component throughout the codebase — e.g. `new Color(0.12f, 0.12f, 0.12f)` in `ImageCarousel`, accordion styles in `CampaignDashboard`. Any visual overhaul would require hunting through every file.

**Recommended approach**

Investigate Godot `.tres` theme resources before doing any visual overhaul. A single theme file can define colours, fonts, and StyleBoxes applied automatically to all matching Control nodes — change once, affects everything. Do this work before touching individual component colours.

---

## Schema Changes

### Task 11 — Add "Acquainted with" NPC relationship seed ✅ Done

"Acquainted with" belongs in the NPC-to-NPC relationship type dropdown, not the party relationship dropdown. Added to `CharacterRelationshipTypeRepository.Defaults` — this is the type used in `_relTypeSelect` on `NpcDetailPane` when linking two NPCs.

> **Note:** Existing campaigns will not receive this seed automatically — `SeedDefaults` only runs on campaign creation. A one-time check-and-insert on campaign load is needed: for each default, if no record with that name exists for this `campaign_id`, insert it.

---

## New Features

### Task 5 — Import / Export ✅ Done

**Settings menu (NavBar `⚙ Settings`)** has four items:

| Item | What it does |
|------|-------------|
| Backup Database | `File.Copy` entire `.db` → chosen path. Backs up all campaigns. |
| Restore Database | `File.Copy` chosen `.db` → app path. Confirms first. Calls `DatabaseService.Reconnect()` then navigates back to campaign list. |
| Export Campaign Data... | Opens `ImportExportModal` in Export mode. User selects any combination of type tables and individual/all entities → saves `.dndx` JSON file. |
| Import Campaign Data... | File picker for `.dndx` → opens `ImportExportModal` in Import mode (pre-checked with file contents) → inserts selected data into current campaign → reloads sidebar. |

**Key files:**
- `Core/Models/ExportPackage.cs` — JSON-serializable container (9 type tables + 6 entity types, including `QuestStatuses` and `Quests`)
- `Core/ImportExportService.cs` — `BuildPackage()` + `ApplyPackage()` with FK remapping by name for types, fresh IDs for entities
- `Scenes/Modals/ImportExportModal/` — modular selection UI using `FoldableContainer` sections with cascade checkboxes

**Type tables exported:** Species, NPC Statuses, NPC Relationship Types, NPC Faction Roles, Character Relationship Types, Location Faction Roles, Faction Relationship Types, Item Types, Quest Statuses

**Entity types exported:** Factions, NPCs, Locations, Sessions, Items, Quests (including `quest_history` entries with session FK remapping)

**Import FK resolution:** Types matched by name (insert if not found). Cross-entity FKs (e.g. NPC → Faction, Quest → NPC/Location) remapped using old→new ID maps; nulled if referenced entity wasn't included in the import. Sessions assigned fresh numbers after the current max to avoid the `UNIQUE(campaign_id, number)` constraint.

---

### Task 13 — Nested locations in sidebar

The data layer for this is already complete. `LocationRepository` already has `GetTopLevel()` and `GetChildren()`. Only the UI needs updating.

**Changes**

In `CampaignDashboard.LoadLocations()`, replace the current `GetAll()` flat list with a recursive tree built from `GetTopLevel()` + `GetChildren()`. Indent child location buttons visually — either using left padding on the StyleBox or by using Godot's `Tree` control instead of a `VBoxContainer`.

> This task and the Parent Location picker in `LocationDetailPane` are the same feature from two angles and should ship together.

---

### Task 14 — Three-column layout: sidebar / detail / wiki panel

The current two-column layout leaves the right side of the screen unused. The target layout is three columns: sidebar navigation left, editable detail fields centre, and an at-a-glance wiki panel right showing images, key facts, and linked entities.

**Approach**

Extend `ApplySidebarWidth()` in `CampaignDashboard` to manage three panels. Suggested proportions: 20% sidebar / 50% detail / 30% wiki panel. Roll out on one entity type first — `LocationDetailPane` is a good candidate — then propagate the pattern.

The wiki panel is the natural home for: faction affiliations, sub-location list, NPC relationships, and last-seen info — as a read-only digest rather than editable fields.

---

### Task 15 — Session detail pane: significant development needed

The current `SessionDetailPane` has: number label, title input, played-on date, wiki notes, image carousel, delete. This is the most-used pane during active play and is the most underdeveloped.

**What is missing**

- Entity tagging panel: structured fields for which NPCs, locations, factions, and quests appeared in this session
- Inline stub creation: ability to create a new NPC or location from within the session pane without losing place
- Wiki link hover preview: hovering a `[[Link]]` shows a preview without navigating away
- Quest status updates: ability to log progress on a quest from within session notes

> The session detail pane redesign and the three-column layout (Task 14) are the same body of work and should be planned together rather than sequentially.

---

### Task 16 — Campaign cover image

Campaigns should support a cover image shown on the campaign card in the selection screen. The `EntityImageRepository` and `ImageCarousel` already work generically for any entity type.

**Change**

Add `Campaign` to the `EntityType` enum in `EntityType.cs`. No other data layer changes required.

---

### Task 17 — Players section: party overview

A Players section on the sidebar for a quick view of the player party. `PlayerCharacter.cs` already exists. Full PC mechanics are deferred to Task 18.

**Scope**

- Add a Players accordion to `CampaignDashboard` alongside the existing five sections
- Detail pane shows: name, species, class, level — basic identifying information only

---

### Task 18 — PC abilities and class features

The original reason for building the app. The Obsidian frontmatter schema maps directly to a SQLite table.

**Schema (new `abilities` table)**

| Column | Type | Notes |
|--------|------|-------|
| `id` | INTEGER PK | Standard |
| `player_character_id` | INTEGER FK | References `player_characters(id)` |
| `name` | TEXT | Ability name |
| `type` | TEXT | e.g. Class Feature, Maneuver, Feat |
| `action` | TEXT | Action, Bonus Action, Reaction, Passive, Free |
| `trigger` | TEXT | Optional — e.g. "When you miss an attack" |
| `cost` | TEXT | Optional — e.g. "1 Superiority Die" |
| `uses` | INTEGER | Maximum uses (0 = unlimited) |
| `uses_remaining` | INTEGER | Current remaining uses |
| `recovery` | TEXT | e.g. "Short Rest", "+1 per Short Rest" |
| `effect` | TEXT | Full description |
| `notes` | TEXT | Rulings and reminders |
| `sort_order` | INTEGER | Display order within the character sheet |

---

### Task 19 — Quests entity and Quest History ✅ Done

A Quests entity on the sidebar for tracking active, completed, and failed quests. Quest History is a sub-feature that logs session-stamped progress entries.

**Implemented files:**
- `Core/Models/Quest.cs`, `QuestHistory.cs`, `QuestStatus.cs`
- `Core/Repositories/QuestRepository.cs`, `QuestHistoryRepository.cs`, `QuestStatusRepository.cs`
- `Core/DatabaseService.cs` — repos registered, migrated, seeded
- `Core/Models/EntityType.cs` — `Quest = 5` added
- `Scenes/Components/QuestDetailPane/QuestDetailPane.cs` + `.tscn`
- `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` + `.tscn` — Quests accordion (pastel teal), `LoadQuests()`, all signal handlers

**Detail pane fields:** Name (select-all on focus), Status (TypeOptionButton), Given by NPC (TypeOptionButton), Location (TypeOptionButton), Reward, Description, WikiNotes, Quest History section with per-entry session picker + note TextEdit + delete, ImageCarousel. Delete via trashcan icon in name row.

**Core `quests` table**

| Column | Type | Notes |
|--------|------|-------|
| `id` | INTEGER PK | Standard |
| `campaign_id` | INTEGER FK | References `campaigns(id)` |
| `name` | TEXT | Quest name |
| `status_id` | INTEGER FK | Seeded type: Active, Completed, Failed, On Hold |
| `description` | TEXT | Quest summary and background |
| `notes` | TEXT | DM-facing secrets |
| `quest_giver_id` | INTEGER FK | Optional — references `characters(id)` |
| `location_id` | INTEGER FK | Optional — references `locations(id)` |
| `reward` | TEXT | Freetext for now |

**`quest_history` table**

| Column | Type | Notes |
|--------|------|-------|
| `id` | INTEGER PK | Standard |
| `quest_id` | INTEGER FK | References `quests(id)` ON DELETE CASCADE |
| `session_id` | INTEGER FK | Optional — references `sessions(id)` |
| `note` | TEXT | What happened to this quest in this session |

---

### Task 20 — NPC–NPC relationships

The `NpcRelationship` model and repository already exist. The open design question is relationship direction.

**Open question**

Relationships can be directional (A knows B, B does not know A) or mutual. The current model supports directionality with from/to NPC fields, but the UI design for displaying and editing this has not been confirmed.

**Recommendation:** treat relationships as directional by default. Display both directions in the UI as separate records if needed — "Sildar is Acquainted with Iarno" does not automatically imply the reverse.

---

### Task 21 — NPC–Location relationship

`Npc.cs` currently has no location fields. The design question is what a location on an NPC actually means, since a single field conflates three distinct concepts.

**The three meanings**

| Meaning | Description |
|---------|-------------|
| Home / Origin | Where they are from or based. Stable. Set during prep. |
| Current Location | Where they are right now. Changes during play. |
| Last Seen | A session-stamped observation: "As of Session 12, spotted in Waterdeep." |

**Options under consideration**

1. Two optional fields on `Npc`: `HomeLocationId` (FK) + `LastSeenNote` (freetext with optional session reference).
2. A join table `npc_locations` with `location_id`, `session_id`, `note` — a full history of where the NPC has been seen.

> No decision made. Option 1 is simpler and covers most DM use cases. Option 2 is more powerful but adds complexity before the need is proven. Revisit when building the NPC detail pane update.

---

### Task 23 — Tab system for the detail pane

The detail pane should support multiple open records simultaneously as tabs, allowing quick switching between e.g. a session and a character sheet without losing either.

**Behaviour**

One tab per record. Tabs are deduplicated — navigating to a record that already has a tab open switches focus to that tab rather than opening a duplicate. This keeps state simple and avoids having to balance multiple instances of the same record.

**Navigation priority when opening a record**

1. If the record is already open in any tab → switch to that tab (pinned or not)
2. Else if the current tab is unpinned → load into the current tab
3. Else if another unpinned tab exists → load into the next unpinned tab
4. Else → open a new tab

**What triggers tab navigation**

- Clicking a sidebar item
- Clicking a `[[wiki link]]` in notes
- Clicking an `EntityRow` component
- Clicking the `+` button (opens a blank tab)

**Tab bar**

- Scrolls horizontally — no cap on tab count
- Auto-scrolls to keep the active tab visible
- `+` button fixed at the end of the tab sequence for explicit new tab creation

**Closing tabs**

- Middle-click anywhere on the tab
- Hover the tab to reveal an `×` button

**Pinning**

- Pinned tabs are never replaced by navigation — navigation skips to the next unpinned tab or opens a new one
- Pin icon to be designed separately
- Visual distinction between pinned and unpinned tabs required (icon state at minimum)

**Files**

New `TabBar` component. Integration into `CampaignDashboard` — the detail panel becomes tab-managed rather than a single pane swap.

---

## Parked Concepts

### Task 22 — History section

Raised during the session in the context of Quests — a log of what happened to a quest over time. Covered by Quest History (Task 19). As a broader concept for other entities (NPC history, Location history), no concrete use case has been defined yet.

Revisit once Quest History is built — if the pattern proves useful it can be generalised.

---

## Project Standards Updates

The following should be ported into `Project_Standards.md` and the Future TODOs table.

### Additive Migration Rule (add to standards)

> Schema changes to tables with existing data must be additive only. No column drops, renames, or type changes once real user data exists. Permitted: new columns with `DEFAULT` values, new join tables, new tables.

### New Future TODOs

| Feature | Priority | Notes |
|---------|----------|-------|
| Import / Export DB | High | Export = `File.Copy` .db. Import = close conn, overwrite, reconnect. Home in navbar. |
| Session detail pane | High | Entity tagging, inline stub creation, wiki hover preview, quest updates. Plan with three-column layout. |
| PC abilities | High | `abilities` table, greenfield. Maps from Obsidian frontmatter schema. |
| Three-column layout | Medium | Sidebar / detail / wiki panel. Start on `LocationDetailPane`, propagate. |
| Standardize image save location | High | Copy images to `imgs/<type>/<id>/` on add. Store relative paths. Prerequisite for image export. (Task 28) |
| Image export/import | Medium | Base64-embed images in `.dndx`. Requires Task 28. Optional "Include Images" checkbox in modal. (Task 29) |
| NPC–Location relationship | Medium | Design open: `HomeLocationId` FK vs `npc_locations` join table. No decision yet. |
| NPC–NPC relationships UI | Medium | Direction TBD. "Acquainted with" seed added to `CharacterRelationshipTypeRepository`; existing campaigns need check-and-insert migration. |
| OS-native file picker | Medium | `DisplayServer.FileDialogShow()` added in Godot 4.3. Check version first. |
| Players section | Medium | Add Players accordion to `CampaignDashboard`. `PlayerCharacter.cs` model exists. |
| Tab system | Medium | One tab per record. Deduplication — navigating to an open record switches to its tab. Scrollable tab bar, pinning, middle-click/hover-× to close. New `TabBar` component. |
| Campaign cover image | Low | Add `Campaign` to `EntityType` enum. Everything else already works generically. |
| Nested locations sidebar | Low | `GetTopLevel()` + `GetChildren()` already exist. Ship with parent picker in `LocationDetailPane`. |
| History section | TBD | Concept only. May be covered by Quest History once built. |

---

---

### Task 24 — Notes consistency: Notes last in all detail panes ✅ Done

The Location detail pane had Notes appearing before the Factions and Sub-locations sections, inconsistent with all other panes.

**Fix (implemented)**

In `location_detail_pane.tscn`, moved `NotesLabel` and `WikiNotes` nodes to after `SubLocationsContainer` — last in `FieldsLeft`. All five detail panes now have Notes as the final field.

---

### Task 25 — Detail pane footer: breathing room at window bottom ✅ Done

Without the app margins, detail pane content butted against the bottom edge of the window.

**Fix (implemented)**

Added `_detailPanel.OffsetBottom = -DetailFooterPadding` (24px) in `CampaignDashboard.ApplySidebarWidth()`.

---

---

### Task 28 — Standardize image save location to `imgs/` folder

Currently, `entity_images.path` stores whatever absolute path the user picked from the OS file dialog. This means:
- Paths are machine-specific (breaks on migration / backup restore to a different machine)
- Images cannot be included in `.dndx` export packages (Task 29 depends on this)
- No predictable folder to back up alongside the `.db` file

**Plan**

When the user picks or drops an image, copy it into a managed folder before recording the path:

```
OS.GetUserDataDir()/imgs/<entity_type>/<entity_id>/<original_filename>
```

Store only the relative path (e.g. `imgs/npc/42/portrait.png`) in the DB. On load, resolve to absolute using `OS.GetUserDataDir()`.

**Changes required**
- `ImageCarousel.cs` — after file picked/dropped, `File.Copy` to the managed path, store relative path
- `EntityImageRepository.cs` — no schema change; just a path convention
- All image display code — resolve relative paths with `OS.GetUserDataDir() + "/" + path` before loading texture
- Existing absolute paths in the DB remain valid (backwards-compatible read: if path starts with `/` or a drive letter, treat as absolute legacy path)

**Folder structure**
```
%APPDATA%/Godot/app_userdata/dnd-builder/
  campaign.db
  imgs/
    npc/42/portrait.png
    location/7/map.jpg
    quest/3/handout.png
```

---

### Task 29 — Image export/import in `.dndx` packages

Currently export/import transfers all text data but leaves images behind. Once Task 28 is done (images in a managed `imgs/` folder), this becomes tractable.

**Approach: embed as base64 in the JSON**

Each exported entity image becomes a record in `ExportPackage.Images` with:
- `EntityType` (string key matching entity type)
- `OldEntityId` (for remapping on import)
- `Filename` (original filename, used to reconstruct the path on import)
- `Data` (base64-encoded bytes of the image file)

On import, for each image record:
1. Decode base64 → write file to `imgs/<entity_type>/<new_entity_id>/<filename>`
2. Insert `entity_images` row with the new relative path and remapped entity ID

**Alternative: zip bundle**

Instead of base64 in JSON, package the `.dndx` file as a `.zip` containing `data.json` + an `imgs/` folder. More efficient for large images but requires a zip library or Godot's `ZIPPacker`. Defer unless base64 sizes become a problem.

**Changes required**
- `ExportPackage.cs` — add `List<ExportedImage> Images`
- `ImportExportService.BuildPackage()` — for each exported entity, load its `entity_images` rows, read file bytes, base64-encode, append to `pkg.Images`
- `ImportExportService.ApplyPackage()` — write image files and insert `entity_images` rows using remapped entity IDs
- `ImportExportModal.cs` — optionally add an "Include Images" checkbox (images can be large; let the user opt in)

> **Dependency:** Task 28 must be complete first. Without a managed `imgs/` folder, there is no reliable way to find or copy image files during export.

---

*Generated March 2026 · TTRPG Companion App · Session Feedback Action Plan · v1.1*
