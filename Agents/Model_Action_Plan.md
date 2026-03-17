# TTRPG Companion App — Model Pre-Alpha Action Plan
*March 2026 · Before First External Share*

---

## Overview

This document captures all model changes required before sharing the app with an alpha tester. The goal is to lock in the correct data structures while zero data exists, avoiding any breaking migrations once a real user starts entering content.

> **Guiding principles**
> 1. Types are seeded but user-editable — enums are replaced with per-campaign models throughout.
> 2. Shared base models prevent duplication — NPCs and Player Characters inherit from Character.
> 3. Join models carry context — flat FK lists are replaced where relationship metadata matters.

---

## Task Summary

| # | Task | Change | Risk if Skipped |
|---|------|--------|-----------------|
| 1 | Remove `Faction.Headquarters` | Delete one field | Orphaned string that can never link to a real Location |
| 2 | Replace `Location.FactionIds` with `LocationFaction` join model | New model + update `Location.cs` | Cannot express relationship type; painful to migrate after data exists |
| 3 | Create `LocationFactionRole` seeded model | New model + seed data | No way to label a faction's presence at a location |
| 4 | Convert `NpcRelationship` enum to seeded model | New model + update `Npc.cs` | Relationship types hardcoded; users cannot customise |
| ★ 5 | Refactor `Npc` into `Character` base + `Npc` + `PlayerCharacter` | New `Character.cs`, update `Npc.cs`, new `PlayerCharacter.cs` | NPCs and PCs cannot share inventory or factions; painful to split later |
| ★ 6 | Convert `NpcStatus` enum to seeded model | New model + update `Npc.cs` | Status types hardcoded; users cannot add campaign-specific states |
| ★ 7 | Add Item system | 4 new models + seed data | No item tracking anywhere in the app |

*★ = added in revised plan*

---

## Task 1 — Remove `Faction.Headquarters`

### What & Why

`Faction.cs` currently has a plain string `Headquarters` field. It cannot link to a real `Location` record, breaks down when a faction operates from multiple locations, and is made redundant by the Location → Faction relationship established in Task 2.

### Change

In `Faction.cs`, delete this line:

```csharp
public string Headquarters { get; set; } = "";
```

No other files need to change for this task.

> **How to find faction headquarters going forward**
> Query Locations whose `LocationFaction` join records include the given `FactionId`. The Location owns the relationship.

---

## Task 2 — Replace `Location.FactionIds` with `LocationFaction` Join Model

### What & Why

`Location.cs` currently holds `List<int> FactionIds` — a flat list with no metadata. This cannot express *why* a faction is associated with a location. Replacing it with a join model adds that context without losing any existing expressiveness.

### New File — `LocationFaction.cs`

```csharp
namespace DndBuilder.Core.Models
{
    public class LocationFaction
    {
        public int LocationId { get; set; }
        public int FactionId  { get; set; }
        public int RoleId     { get; set; }  // FK -> LocationFactionRole.Id
    }
}
```

### Update `Location.cs`

Remove:
```csharp
public List<int> FactionIds { get; set; } = new();
```

Add:
```csharp
public List<LocationFaction> Factions { get; set; } = new();
```

> **Repository note:** Any repo or query that reads/writes `FactionIds` on a Location will need updating to use the `Factions` collection and the `location_factions` join table in SQLite.

---

## Task 3 — Create `LocationFactionRole` Seeded Model

### What & Why

The `RoleId` on `LocationFaction` needs to point somewhere. Rather than a hardcoded enum, this is a per-campaign table seeded with sensible defaults on campaign creation but fully editable by the user. Follows the same pattern as `Species`.

### New File — `LocationFactionRole.cs`

```csharp
namespace DndBuilder.Core.Models
{
    public class LocationFactionRole
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

### Seed Data

| Name | Description |
|------|-------------|
| Present | Generally associated with or operating out of this location |
| Controls | Owns or runs this location |
| Occupies | Militarily or forcibly holding this location |
| Opposes | Actively working against this location or its inhabitants |
| Hidden | Secret or covert presence — not publicly known |

> **UI work required (later):** A screen to manage `LocationFactionRole` records per campaign will be needed. Seeded defaults are sufficient for the alpha.

---

## Task 4 — Convert `NpcRelationship` Enum to Seeded Model

### What & Why

`NpcRelationship` is currently a hardcoded C# enum. Users cannot add types like Mentor, Rival, or Romantically Involved. Converting it to a per-campaign model follows the same pattern as `Species` and `LocationFactionRole`.

### New File — `NpcRelationshipType.cs`

Create this file and delete the existing `NpcRelationship.cs` enum file.

```csharp
namespace DndBuilder.Core.Models
{
    public class NpcRelationshipType
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

### Update `Npc.cs`

Remove:
```csharp
public NpcRelationship Relationship { get; set; } = NpcRelationship.Neutral;
```

Add:
```csharp
public int? RelationshipTypeId { get; set; }  // FK -> NpcRelationshipType.Id
```

Also remove any using/namespace reference to the old `NpcRelationship` enum.

### Seed Data

| Name | Description |
|------|-------------|
| Hostile | Actively hostile; will attack or obstruct the party |
| Unfriendly | Distrustful or uncooperative |
| Neutral | No strong feelings either way |
| Friendly | Cooperative and well-disposed toward the party |
| Allied | Actively supportive; will assist the party when possible |

> **UI work required (later):** A screen to manage `NpcRelationshipType` records per campaign will be needed. Deferred until after the alpha.

---

## Task 5 — Refactor `Npc` into `Character` Base + `Npc` + `PlayerCharacter`

### What & Why

NPCs and Player Characters are both characters in the world and share a large amount of common data — name, species, gender, occupation, portrait, factions. Keeping them as separate top-level models causes duplication and prevents shared functionality like inventory. A shared base model solves this cleanly while inheritance keeps NPC-specific and PC-specific fields separated.

### SQLite Table Structure — Table-Per-Type

Three tables: `characters` (shared base fields), `npcs` (NPC-specific fields + `CharacterId` FK), `player_characters` (PC-specific fields + `CharacterId` FK). Loading an NPC means joining `npcs` → `characters` on Id. No discriminator columns, no nulls for irrelevant fields.

### New File — `Character.cs` (shared base)

```csharp
using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Character
    {
        public int         Id           { get; set; }
        public int         CampaignId   { get; set; }
        public string      Name         { get; set; } = "";
        public string      PortraitPath { get; set; } = "";
        public string      Gender       { get; set; } = "";
        public string      Occupation   { get; set; } = "";
        public string      Description  { get; set; } = "";  // Appearance / first impression
        public string      Personality  { get; set; } = "";
        public string      Notes        { get; set; } = "";
        public int?        SpeciesId    { get; set; }
        public List<int>   FactionIds   { get; set; } = new();
    }
}
```

### Update `Npc.cs` (extends Character)

Strip everything that moved to `Character`. What remains is NPC-specific:

```csharp
namespace DndBuilder.Core.Models
{
    public class Npc : Character
    {
        public int?  HomeLocationId    { get; set; }
        public int?  FirstSeenSession  { get; set; }
        public int?  RelationshipTypeId { get; set; }  // FK -> NpcRelationshipType.Id
        public int?  StatusId           { get; set; }  // FK -> NpcStatus.Id
    }
}
```

### New File — `PlayerCharacter.cs` (extends Character)

Placeholder for now. System-specific sheet data (DnD 5.5e stats, etc.) is added in Phase 2.

```csharp
namespace DndBuilder.Core.Models
{
    public class PlayerCharacter : Character
    {
        // System-specific sheet fields added in Phase 2
    }
}
```

### Fields Removed from `Npc.cs`

| Field | Moved To |
|-------|----------|
| `Name` | `Character.cs` |
| `PortraitPath` | `Character.cs` |
| `Gender` | `Character.cs` |
| `Occupation` | `Character.cs` |
| `Description` | `Character.cs` |
| `Personality` | `Character.cs` |
| `Notes` | `Character.cs` |
| `SpeciesId` | `Character.cs` |
| `FactionIds` | `Character.cs` |
| `Relationship` | Removed — replaced by `RelationshipTypeId` (Task 4) |
| `Status` | Removed — replaced by `StatusId` (Task 6) |

> **Repository note:** Repositories currently referencing `Npc` will need to be updated to account for the base `characters` table. Any `CharacterItem` queries (Task 7) use the shared `Character.Id`.

---

## Task 6 — Convert `NpcStatus` Enum to Seeded Model

### What & Why

`NpcStatus` is currently a hardcoded C# enum. Campaign-specific states like Transformed, Imprisoned, Exiled, or Ascended cannot be added. Following the same pattern as `NpcRelationshipType`, this becomes a per-campaign seeded model.

### New File — `NpcStatus.cs` (replaces enum)

Delete the existing `NpcStatus.cs` enum and replace with:

```csharp
namespace DndBuilder.Core.Models
{
    public class NpcStatus
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

### Seed Data

| Name | Description |
|------|-------------|
| Unknown | Status not yet established |
| Alive | Currently living |
| Dead | Deceased |
| Missing | Whereabouts unknown |
| Captured | Held against their will |

> **UI work required (later):** A screen to manage `NpcStatus` records per campaign will be needed alongside `NpcRelationshipType` and `LocationFactionRole`.

---

## Task 7 — Add Item System

### What & Why

Items were missing from the model entirely. Every campaign needs to track weapons, armor, trinkets, key items, and unique artifacts. The system uses a three-part structure: a system-agnostic `Item`, a `SystemItem` bridge connecting it to a game system, and system-specific mechanics tables (one per supported system). Two join models associate items with Characters and Locations.

### New File — `Item.cs`

```csharp
namespace DndBuilder.Core.Models
{
    public class Item
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Notes       { get; set; } = "";  // DM secrets / history
        public bool   IsUnique    { get; set; } = false;
        public int?   TypeId      { get; set; }        // FK -> ItemType.Id
    }
}
```

> **On `IsUnique`:** Unique means this item has its own identity and history — it is a named, one-of-a-kind object in the world (e.g. *The Battered Blade of Isle Dartmor*), not a generic instance. The repository layer can use this flag to warn if a unique item is assigned to more than one character or location simultaneously.

### New File — `ItemType.cs`

Seeded per campaign, user-editable. Same pattern as `LocationFactionRole` and `NpcStatus`.

```csharp
namespace DndBuilder.Core.Models
{
    public class ItemType
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

### ItemType Seed Data

| Name | Description |
|------|-------------|
| Weapon | Swords, axes, bows, and other offensive items |
| Armor | Worn protective equipment |
| Consumable | Single-use items such as potions and scrolls |
| Trinket | Minor keepsakes and curiosities with no mechanical use |
| Document | Letters, maps, contracts, and written materials |
| Treasure | Coins, gems, art objects, and valuable goods |
| Key Item | Plot-critical items tied to quests or story beats |
| Misc | Anything that does not fit another category |

### New File — `SystemItem.cs` (bridge)

Connects a core `Item` to a specific game system. One record per system that recognises this item.

```csharp
namespace DndBuilder.Core.Models
{
    public class SystemItem
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }  // Direct campaign scope for fast queries
        public int    ItemId     { get; set; }  // FK -> Item.Id
        public string System     { get; set; } = "";  // e.g. "dnd5e_2024"
    }
}
```

### System-Specific Mechanics — `DnD5eItemMechanics` (stub)

Each supported game system gets its own mechanics table. The DnD5e version is defined when the character sheet module is built in Phase 2.

```csharp
namespace DndBuilder.Core.Models
{
    public class DnD5eItemMechanics
    {
        public int Id           { get; set; }
        public int SystemItemId { get; set; }  // FK -> SystemItem.Id
        // Full field list defined in Phase 2. Expected fields:
        // public string Rarity             { get; set; } = "";
        // public bool   RequiresAttunement { get; set; } = false;
        // public string DamageDice         { get; set; } = "";
        // public string DamageType         { get; set; } = "";
        // public float  Weight             { get; set; } = 0;
        // public int    BonusModifier      { get; set; } = 0;
        // public string Properties         { get; set; } = "";
    }
}
```

> **Three-part item system:** `Item` = flavor and identity. `SystemItem` = bridge to a game system. `DnD5eItemMechanics` = system-specific rules. An axe in a DnD5e campaign: one `Item`, one `SystemItem` (System = `"dnd5e_2024"`), one `DnD5eItemMechanics`. Items with no mechanical rules (a letter, a trinket) need the `Item` record only.

> **Adding a new system:** Create a new mechanics table with the system prefix (e.g. `pathfinder_item_mechanics`). No changes to `Item` or `SystemItem`.

> **DnD5eItemMechanics fields deferred:** The full column list is defined when the character sheet module is built in Phase 2. Do not finalise the `dnd5e_item_mechanics` table before then.

### New File — `CharacterItem.cs`

Links an `Item` to a `Character` (NPC or Player Character). No metadata needed for the alpha.

```csharp
namespace DndBuilder.Core.Models
{
    public class CharacterItem
    {
        public int CharacterId { get; set; }  // FK -> characters.Id
        public int ItemId      { get; set; }  // FK -> Item.Id
    }
}
```

### New File — `LocationItem.cs`

Links an `Item` to a `Location`. Covers treasure hoards, items on display, loot in a dungeon room, etc.

```csharp
namespace DndBuilder.Core.Models
{
    public class LocationItem
    {
        public int LocationId { get; set; }  // FK -> Location.Id
        public int ItemId     { get; set; }  // FK -> Item.Id
    }
}
```

---

## SQLite Naming Conventions

SQLite does not support schemas. Logical grouping is enforced through a table name prefix convention. All table names are `snake_case`. Full details are in the Project Standards document.

| Prefix | Scope | Examples |
|--------|-------|---------|
| `` | Generic / system-agnostic core | `campaigns`, `characters`, `items` |
| `dnd5e_` | D&D 5.5e (2024) module | `dnd5e_character_sheet`, `dnd5e_item_mechanics` |
| `pathfinder_` | Pathfinder module (future) | `pathfinder_character_sheet`, `pathfinder_item_mechanics` |

---

## What Is NOT Changing

The following were reviewed and confirmed correct for the alpha:

| Model / Field | Status |
|---------------|--------|
| `Location.ParentLocationId` + `SubLocations` | Correct — self-referencing hierarchy in place |
| `Npc.HomeLocationId` | Correct — soft link to location, moves to `Npc` subclass |
| `Npc.FirstSeenSession` | Correct — useful traceability, moves to `Npc` subclass |
| `Species.CampaignId` | Intentional — per-campaign, supports homebrew species |
| Campaign as top-level FK | Correct — used consistently throughout |
| Session model | Half-baked by design — deferred to Phase 4 linking layer |
| `Campaign.DateStarted` as string | Deferred — `DateTime?` migration to be decided later |
| Wiki link editor | Deferred — typed links / intent parsing is Phase 4+ |

---

## Deferred UI Work

The following screens will be needed after the alpha to let users manage seeded data. None are required to ship the alpha — seed data on campaign creation is sufficient.

| Screen | Manages |
|--------|---------|
| Campaign Settings — Faction Roles | `LocationFactionRole` records per campaign |
| Campaign Settings — NPC Relationships | `NpcRelationshipType` records per campaign |
| Campaign Settings — NPC Statuses | `NpcStatus` records per campaign |
| Campaign Settings — Item Types | `ItemType` records per campaign |
| Campaign Settings — Species | Already planned — `Species` per campaign |

> **Suggestion:** These five screens are nearly identical in structure. A single generic Campaign Settings UI with a tab per type would avoid building the same screen five times.

---

*Generated March 2026 · TTRPG Companion App · Pre-Alpha Model Changes · v4.0*
