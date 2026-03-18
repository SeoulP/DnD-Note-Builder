# TTRPG Companion App — Project Standards & Conventions
*March 2026 · v1.0 · Living Document*

---

## Purpose

This document defines the standards, naming conventions, and architectural decisions for the TTRPG Companion App. It is a living document — update it when a new decision is made rather than relying on tribal knowledge. When in doubt about how to name something or where something belongs, check here first.

---

## Database Conventions

### Engine

SQLite via `Microsoft.Data.Sqlite` (NuGet). The `.db` file is the entire save — portable by design. Cloud sync can be added later by syncing the file; no app code changes required.

### Additive Migration Rule

Once real user data exists in a table, schema changes to that table must be additive only. This is a hard rule, not a guideline.

**Permitted:**
- New columns with `DEFAULT` values
- New join tables
- New tables

**Prohibited:**
- Column drops
- Column renames
- Column type changes
- Table drops or renames

> **Why:** The `.db` file is the user's only save. A destructive migration that runs against real data cannot be undone. When in doubt, add — never remove.

### Schema Simulation via Table Naming

SQLite does not support schemas. Logical grouping is enforced through a table name prefix convention. All table names are `snake_case`. Core system-agnostic tables use no prefix; system-specific tables use a system prefix.

| Prefix | Scope | Examples |
|--------|-------|---------|
| *(none)* | Generic / system-agnostic core | `campaigns`, `characters`, `items`, `locations` |
| `dnd5e_` | D&D 5.5e (2024) module | `dnd5e_character_sheet`, `dnd5e_item_mechanics`, `dnd5e_spells` |
| `pathfinder_` | Pathfinder module (future) | `pathfinder_character_sheet`, `pathfinder_item_mechanics` |

> **Rule:** If a table belongs to the core system-agnostic model, it gets the `` prefix. If it belongs to a specific game system module, it gets that system's prefix. Never mix concerns across prefixes.

### Column Naming

Column names are PascalCase in C# model properties and `snake_case` in raw SQL. The SQLite layer handles the mapping.

| Pattern | Example | Notes |
|---------|---------|-------|
| Primary key | `Id` | Always `int`, always named `Id` |
| Foreign key | `CampaignId`, `ItemId` | ParentType + Id |
| Nullable foreign key | `int? HomeLocationId` | Nullable int for optional relationships |
| System string | `System` | Matches `Campaign.System` — e.g. `"dnd5e_2024"` |
| Boolean flags | `IsUnique`, `IsActive` | `Is` prefix, PascalCase |
| Date strings | `DateStarted`, `PlayedOn` | ISO-8601 string for now — `DateTime?` migration TBD |

### Join Tables

Join tables that carry no metadata beyond the two foreign keys are named `ParentTypeChildType` in PascalCase C# and `parent_type_child_type` in SQLite. Join tables that carry metadata get a proper model name.

| Model | SQLite Table | Notes |
|-------|-------------|-------|
| `CharacterItem` | `character_items` | Simple join — no metadata |
| `LocationItem` | `location_items` | Simple join — no metadata |
| `LocationFaction` | `location_factions` | Carries `RoleId` — has metadata |
| `SystemItem` | `system_items` | Bridge between `Item` and system mechanics |

### Seeded Per-Campaign Tables

Several tables are seeded with default values when a campaign is created and are fully editable by the user. These always have `Id` and `CampaignId` as their first two columns. The seed data defined in the action plan is the canonical starting point.

| Model | SQLite Table |
|-------|-------------|
| `Species` | `species` |
| `ItemType` | `item_types` |
| `LocationFactionRole` | `location_faction_roles` |
| `NpcRelationshipType` | `npc_relationship_types` |
| `NpcStatus` | `npc_statuses` |

---

## Model Conventions

### Namespace

All models live in `DndBuilder.Core.Models` regardless of whether they are generic or system-specific. System-specific models are distinguished by their class name prefix (e.g. `DnD5eItemMechanics`) and their corresponding `dnd5e_` table prefix in SQLite.

### Inheritance Pattern — Table-Per-Type

Shared base models use Table-Per-Type inheritance in SQLite. Each level of the hierarchy has its own table. Child tables reference the parent by `Id`.

| C# Class | SQLite Table | Notes |
|----------|-------------|-------|
| `Character` | `characters` | Shared base — name, portrait, species, factions |
| `Npc` | `npcs` | `CharacterId` FK + NPC-specific fields |
| `PlayerCharacter` | `player_characters` | `CharacterId` FK + placeholder for sheet data |

> **Loading rule:** Loading an NPC always means joining `npcs` → `characters` on `Id`. Never read from `npcs` alone — the base fields will be missing.

### System-Specific Module Pattern

System-specific data extends core models through a bridge table and a mechanics table. This pattern applies to both characters and items.

| Layer | Example | Purpose |
|-------|---------|---------|
| Core | `Item` | System-agnostic identity and flavor |
| Bridge | `SystemItem` | Connects core record to a specific game system |
| Mechanics | `DnD5eItemMechanics` | System-specific rules — one table per supported system |

> **Adding a new system:** Create a new mechanics table with the system prefix (e.g. `pathfinder_item_mechanics`). No changes to core or bridge tables. The `System` string column on the bridge table identifies which mechanics table to join.

### Campaign Scoping

Every top-level entity has a `CampaignId`. Everything else is reached through its parent. The rule: if it lives in the world of a campaign, it has a `CampaignId`. If it is a join table between two already-scoped entities, it does not need one.

| Model | Has `CampaignId`? | Reason |
|-------|-------------------|--------|
| `Campaign` | N/A | Is the top-level container |
| `Character` / `Npc` / `PlayerCharacter` | Yes | Top-level entity |
| `Item` | Yes | Top-level entity |
| `SystemItem` | Yes | Direct campaign query support without joining through `Item` |
| `Location` | Yes | Top-level entity |
| `Faction` | Yes | Top-level entity |
| `Session` | Yes | Top-level entity |
| `CharacterItem` | No | Scoped through `Character.CampaignId` |
| `LocationFaction` | No | Scoped through `Location.CampaignId` |
| `LocationItem` | No | Scoped through `Location.CampaignId` |
| Seeded type tables | Yes | Always per-campaign — user-editable |

---

## Project Structure

### Godot / C# Layout

```
GodotApp/
  Modules/
    Notes/                     — Sessions, Locations, Factions, NPCs
    Characters/
      Core/                    — Character, Npc, PlayerCharacter models
      Sheets/
        DnD5e/                 — DnD 5.5e character sheet + item mechanics
        Pathfinder/            — Future
    Items/
      Core/                    — Item, SystemItem, ItemType
      Systems/
        DnD5e/                 — DnD5eItemMechanics
        Pathfinder/            — Future
  Data/
    Database.cs                — SQLite connection layer
    Repositories/              — One repo per top-level model
    Seed/                      — Campaign creation seed data
  UI/
    Shared/                    — Navigation, search, linking
    Settings/                  — Campaign settings screens (type editors)
```

### Repository Pattern

One repository per top-level model. Repositories are responsible for all SQL and for assembling joined models (e.g. loading an `Npc` means the `NpcRepository` joins `npcs` and `characters`). Business logic does not live in repositories.

---

## Sidebar Conventions

### Customisation Model

The sidebar is user-configurable per campaign. Each section (Factions, Locations, NPCs, Sessions, Items, etc.) can be shown or hidden, and sections can be reordered. The configuration is stored per campaign so different campaigns can have different sidebar layouts.

| Property | Details |
|----------|---------|
| Visibility | Each section has an `IsVisible` bool. Hidden sections are not removed — their data is intact. |
| Order | Each section has an integer `SortIndex`. Lower = higher in the sidebar. |
| Input method | Drag-and-drop preferred. Index-based fallback acceptable for v1. |
| Scope | Per campaign. Sidebar layout is saved with the campaign, not globally. |
| Defaults | All sections visible, default order: Sessions, Locations, NPCs, Factions, Items. Adjust as UX evolves. |

> **Storage:** Sidebar config can be stored as a `sidebar_config` table (`CampaignId`, `SectionKey`, `IsVisible`, `SortIndex`) or serialised as JSON in a campaign settings column. Table is cleaner and queryable — recommended.

---

## Tab System Conventions

The detail pane supports multiple open records simultaneously as tabs.

### Core Rules

- **One tab per record.** A record can only be open in one tab at a time.
- **No duplicates.** Navigating to a record that already has a tab switches focus to that tab rather than opening a second instance. This applies regardless of whether the existing tab is pinned.

### Navigation Priority

When navigating to a record (via sidebar, wiki link, entity row, or + button):

1. If the record is already open in any tab → switch to that tab
2. Else if the current tab is unpinned → load into the current tab
3. Else if another unpinned tab exists → load into the next unpinned tab
4. Else → open a new tab

### What Triggers Tab Navigation

- Sidebar item click
- `[[Wiki link]]` click in notes
- `EntityRow` component click
- `+` button (opens a blank tab explicitly)

### Tab Bar

- Scrolls horizontally — no cap on tab count
- Auto-scrolls to keep the active tab visible
- `+` button fixed at the end of the tab sequence

### Closing Tabs

- Middle-click anywhere on the tab
- Hover the tab to reveal an `×` button

### Pinning

- Pinned tabs are never replaced by navigation
- Navigation skips pinned tabs and follows the priority order above
- Pin icon to be designed separately
- Pinned and unpinned tabs must be visually distinct (icon state at minimum)

---

## Future TODOs

Tracked here so nothing gets lost. Items are added as decisions are made during development. Priority is approximate.

| Feature | Priority | Notes |
|---------|----------|-------|
| Campaign Settings — Type Editors | High | UI screens to manage seeded-but-editable types per campaign: Species, ItemType, LocationFactionRole, NpcRelationshipType, NpcStatus. These are nearly identical in structure — build a single generic screen with a tab or dropdown per type rather than five separate screens. |
| Import / Export DB | High | Export = `File.Copy` the `.db` to a user-chosen location. Import = close SQLite connection, overwrite `.db`, reconnect. Natural home in the navbar. Show a confirmation prompt before import. |
| Session detail pane | High | Needs: entity tagging panel (NPCs/locations/factions/quests present), inline stub creation, wiki link hover preview, quest status updates. Plan alongside the three-column layout — they are the same body of work. |
| PC abilities / class features | High | New `abilities` table. Greenfield. Schema maps directly from the Obsidian frontmatter pattern (uses, uses_remaining, recovery, action, cost, effect). Linked to `player_character_id`. |
| Tab system | Medium | One tab per record. Deduplication — navigating to an open record switches to its tab. Scrollable tab bar, pinning, middle-click / hover-× to close. New `TabBar` component integrated into `CampaignDashboard`. Full spec in Tab System Conventions section above. |
| Sidebar Customisation | Medium | Per-campaign show/hide and reordering of sidebar sections. Drag-and-drop preferred; index-based input acceptable for v1. Store in `sidebar_config` (`CampaignId`, `SectionKey`, `IsVisible`, `SortIndex`). |
| Three-column layout | Medium | Sidebar / detail / wiki panel. Extend `ApplySidebarWidth()` to a three-way split. Start on `LocationDetailPane`, propagate. Wiki panel is read-only digest: faction affiliations, sub-locations, relationships, last-seen info. |
| Quests entity | Medium | New `quests` + `quest_history` tables. Quest giver FK to `characters`, location FK, seeded status type (Active, Completed, Failed, On Hold). Quest History: `quest_id`, `session_id`, `note`. |
| NPC–Location relationship | Medium | Design open. `Npc.cs` has no location fields. Options: (1) `HomeLocationId` FK + `LastSeenNote` freetext, (2) `npc_locations` join table with `location_id`, `session_id`, `note`. No decision yet — revisit when building the NPC detail pane update. |
| NPC–NPC relationships UI | Medium | `NpcRelationship` model exists. Direction TBD — treat as directional by default. "Acquainted with" seed needs a one-time check-and-insert migration for existing campaigns. |
| OS-native file picker | Medium | `DisplayServer.FileDialogShow()` added in Godot 4.3 — calls the OS dialog natively, no plugin needed. Check project Godot version first. |
| Players section | Medium | Add Players accordion to `CampaignDashboard`. `PlayerCharacter.cs` model already exists. Basic display: name, species, class, level. |
| Copy / Paste Between Campaigns | Medium | Allow copying an entity (NPC, Item, Location, etc.) from one campaign to another. System-specific data must be dropped on copy if the destination campaign uses a different system. Show a clear warning before confirming. |
| Nested locations sidebar | Low | `GetTopLevel()` and `GetChildren()` already exist in `LocationRepository`. Switch `LoadLocations()` from `GetAll()` to recursive tree. Ship together with the parent picker in `LocationDetailPane`. |
| Campaign cover image | Low | Add `Campaign` to the `EntityType` enum. `EntityImageRepository` and `ImageCarousel` already work generically — no other data layer changes needed. |
| Godot Theme resource | Low | Investigate `.tres` theme resource before any visual overhaul. Centralises colours, fonts, and StyleBoxes. Change once, affects everything. |
| Wiki Link Typed Relationships | Low | Upgrade the wiki link editor so links carry intent (e.g. `[[Phandalin\|lives in]]`). Parsed links create structured relationships. Phase 4 linking layer territory. |
| DnD 5.5e ItemDefinition Fields | Phase 2 | Define the full `dnd5e_item_mechanics` column list (Rarity, RequiresAttunement, DamageDice, DamageType, Weight, BonusModifier, Properties). Deferred until the character sheet module is built. |
| Markdown Rendering | Phase 6+ | Notes fields currently plain text. Render markdown in read mode. Godot has no built-in markdown renderer — requires a custom implementation or third-party solution. |
| Portraits | Phase 6+ | `PortraitPath` field already exists on `Character`. UI for uploading and displaying portraits is deferred. Now superseded by the `EntityImage` carousel system — `PortraitPath` is legacy. |
| Cloud Sync | Phase 6+ | Dropbox / Google Drive sync by syncing the `.db` file. No app code changes required — purely an onboarding / documentation task. |
| History section | TBD | Concept only. May be covered by Quest History once built. If the pattern proves useful it can be generalised to NPCs, Locations, etc. |
| `DateTime` Migration | TBD | `Campaign.DateStarted` and `Session.PlayedOn` are currently stored as ISO-8601 strings. Decide whether to migrate to `DateTime?` for proper sorting and filtering. |
| Pathfinder Support | TBD | Add `pathfinder_character_sheet` and `pathfinder_item_mechanics` tables. No core model changes required — add `Campaign.System` value and new module folder. |

---

*Generated March 2026 · TTRPG Companion App · Project Standards v1.1 · Living Document*
