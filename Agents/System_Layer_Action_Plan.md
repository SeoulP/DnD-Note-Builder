# System Layer — Action Plan
*March 2026 · v1.0*

---

## Overview

A new **System** panel sits alongside the existing **Notes** panel, accessible via two new Navbar buttons. Both panels use the same sidebar + detail pane + reference panel layout. The System panel manages campaign-scoped ruleset data (Classes, Species, Abilities) that Notes entities (NPCs, Player Characters) reference via foreign keys.

The existing "image carousel column" is renamed to **Reference Panel** everywhere — it already shows images + related links in Sessions, and will show contextually relevant info per entity type going forward.

---

## Status Key

| Symbol | Meaning |
|--------|---------|
| ✅ | Complete |
| ⬜ | To do |
| 🔶 | In progress |
| 🚫 | Parked / no decision |

---

## Task Summary

| # | Task | Area | Priority | Status |
|---|------|------|----------|--------|
| S1 | Navbar — Notes + System buttons | UI | High | ⬜ |
| S2 | SystemPanel shell scene | UI | High | ⬜ |
| S3 | Ability — model, repo, detail pane | Data+UI | High | ⬜ |
| S4 | Class + Subclass — model, repo, detail pane | Data+UI | High | ⬜ |
| S5 | Species expansion + Subspecies — model, repo, detail pane | Data+UI | High | ⬜ |
| S6 | Ability link tables (class, subclass, species, subspecies) | Data | High | ⬜ |
| S7 | Player Characters in Notes — class/subclass/subspecies FKs, level | Data+UI | High | ⬜ |
| S8 | PC ability propagation + character_abilities use tracking | Data+UI | High | ⬜ |
| S9 | Seeding — D&D 5e classes, subclasses, species, subspecies, abilities | Data | Medium | ⬜ |
| S10 | Rename Reference Panel everywhere (ImageCarousel column) | UX | Low | ⬜ |
| S11 | Species migration — move dropdown sources from Notes seeds to System | Data | High | ⬜ |

---

## Architecture

### Navbar

```
[← Campaign Name]  [Notes]  [System]  ...  [Settings ▾]
```

- **Notes** button → shows existing `CampaignDashboard` scene (NPCs, Factions, Locations, Sessions, Items, Quests, Players)
- **System** button → shows new `SystemPanel` scene (Classes, Species, Abilities)
- Both panels are per-campaign; switching campaigns resets both
- Active panel button is highlighted

### Panel Layout (both Notes and System)

```
┌─────────────────────────────────────────────────────┐
│ Navbar                                              │
├──────────────┬──────────────────────┬───────────────┤
│              │                      │               │
│   Sidebar    │    Detail Pane       │  Reference    │
│  (accordion  │  (scrollable form)   │   Panel       │
│   sections)  │                      │  (images +    │
│              │                      │   context)    │
└──────────────┴──────────────────────┴───────────────┘
```

The Reference Panel is the renamed third column. In most panes it shows the ImageCarousel. In Sessions it already shows images + related links. Future panes can add contextual content there (e.g. ability cross-references, class members, etc.).

---

## Database Schema

### New Tables

#### `classes`
```sql
CREATE TABLE IF NOT EXISTS classes (
    id          INTEGER PRIMARY KEY,
    campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    name        TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    notes       TEXT    NOT NULL DEFAULT '',
    sort_order  INTEGER NOT NULL DEFAULT 0
)
```

#### `subclasses`
```sql
CREATE TABLE IF NOT EXISTS subclasses (
    id          INTEGER PRIMARY KEY,
    campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    class_id    INTEGER NOT NULL REFERENCES classes(id)   ON DELETE CASCADE,
    name        TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    notes       TEXT    NOT NULL DEFAULT '',
    sort_order  INTEGER NOT NULL DEFAULT 0
)
```

#### `subspecies`
```sql
CREATE TABLE IF NOT EXISTS subspecies (
    id          INTEGER PRIMARY KEY,
    campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    species_id  INTEGER NOT NULL REFERENCES species(id)   ON DELETE CASCADE,
    name        TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    notes       TEXT    NOT NULL DEFAULT ''
)
```

#### `abilities`
```sql
CREATE TABLE IF NOT EXISTS abilities (
    id          INTEGER PRIMARY KEY,
    campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    name        TEXT    NOT NULL DEFAULT '',
    type        TEXT    NOT NULL DEFAULT '',  -- Class Feature / Feat / Racial / Subclass Feature / etc.
    action      TEXT    NOT NULL DEFAULT '',  -- Action / Bonus Action / Reaction / Passive / Free
    trigger     TEXT    NOT NULL DEFAULT '',
    cost        TEXT    NOT NULL DEFAULT '',
    uses        INTEGER NOT NULL DEFAULT 0,   -- template max (0 = unlimited)
    recovery    TEXT    NOT NULL DEFAULT '',  -- Short Rest / Long Rest / etc.
    effect      TEXT    NOT NULL DEFAULT '',
    notes       TEXT    NOT NULL DEFAULT '',
    sort_order  INTEGER NOT NULL DEFAULT 0
)
-- NOTE: No uses_remaining here — that lives on character_abilities per PC
```

#### Ability Link Tables (join, no metadata)
```sql
CREATE TABLE IF NOT EXISTS class_abilities (
    class_id   INTEGER NOT NULL REFERENCES classes(id)   ON DELETE CASCADE,
    ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
    PRIMARY KEY (class_id, ability_id)
)

CREATE TABLE IF NOT EXISTS subclass_abilities (
    subclass_id INTEGER NOT NULL REFERENCES subclasses(id) ON DELETE CASCADE,
    ability_id  INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
    PRIMARY KEY (subclass_id, ability_id)
)

CREATE TABLE IF NOT EXISTS species_abilities (
    species_id INTEGER NOT NULL REFERENCES species(id)   ON DELETE CASCADE,
    ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
    PRIMARY KEY (species_id, ability_id)
)

CREATE TABLE IF NOT EXISTS subspecies_abilities (
    subspecies_id INTEGER NOT NULL REFERENCES subspecies(id) ON DELETE CASCADE,
    ability_id    INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
    PRIMARY KEY (subspecies_id, ability_id)
)
```

#### `character_abilities` — PC use tracking
```sql
CREATE TABLE IF NOT EXISTS character_abilities (
    character_id   INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    ability_id     INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
    uses_remaining INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (character_id, ability_id)
)
```

### Additive Migrations on Existing Tables

#### `species` — expand in place
```sql
ALTER TABLE species ADD COLUMN description TEXT NOT NULL DEFAULT ''
ALTER TABLE species ADD COLUMN notes       TEXT NOT NULL DEFAULT ''
-- Existing name-only rows remain valid; dropdowns continue to work unchanged
```

#### `player_characters` — add system references + level
```sql
ALTER TABLE player_characters ADD COLUMN class_id      INTEGER REFERENCES classes(id)    ON DELETE SET NULL
ALTER TABLE player_characters ADD COLUMN subclass_id   INTEGER REFERENCES subclasses(id) ON DELETE SET NULL
ALTER TABLE player_characters ADD COLUMN subspecies_id INTEGER REFERENCES subspecies(id) ON DELETE SET NULL
ALTER TABLE player_characters ADD COLUMN level         INTEGER NOT NULL DEFAULT 1
-- species_id already exists on characters table via characters.species_id
```

### EntityType Enum
```csharp
PlayerCharacter = 6,  // add
Class           = 7,  // add
Species         = 8,  // add (was seeded type, now full entity)
Ability         = 9,  // add
```

---

## Models

### New Models
| File | Class | Notes |
|------|-------|-------|
| `Core/Models/Ability.cs` | `Ability` | System-level definition; no uses_remaining |
| `Core/Models/Class.cs` | `Class` | Includes `List<Subclass> Subclasses` when loaded via Get() |
| `Core/Models/Subclass.cs` | `Subclass` | ClassId FK |
| `Core/Models/Subspecies.cs` | `Subspecies` | SpeciesId FK |
| `Core/Models/CharacterAbility.cs` | `CharacterAbility` | PC use tracking: CharacterId, AbilityId, UsesRemaining |

### Modified Models
| File | Changes |
|------|---------|
| `Core/Models/Species.cs` | Add `Description`, `Notes` fields |
| `Core/Models/PlayerCharacter.cs` | Add `ClassId?`, `SubclassId?`, `SubspeciesId?`, `Level`; add `List<CharacterAbility> Abilities` |

---

## Repositories

### New Repositories
| File | Responsibility |
|------|---------------|
| `Core/Repositories/ClassRepository.cs` | CRUD for `classes` + `subclasses`; migrate `class_abilities`, `subclass_abilities` |
| `Core/Repositories/AbilityRepository.cs` | CRUD for `abilities`; migrate `class_abilities`, `subclass_abilities`, `species_abilities`, `subspecies_abilities`, `character_abilities` |
| `Core/Repositories/SubspeciesRepository.cs` | CRUD for `subspecies`; migrate `subspecies_abilities` |
| `Core/Repositories/PlayerCharacterRepository.cs` | CRUD for `player_characters` (joins `characters`); additive column migrations for class_id, subclass_id, subspecies_id, level |

### Modified Repositories
| File | Changes |
|------|---------|
| `Core/Repositories/SpeciesRepository.cs` | Add `description`/`notes` columns to Migrate(); expand Map(); add Edit() if missing |
| `Core/Repositories/NpcRepository.cs` | `player_characters` table creation moves to `PlayerCharacterRepository`; NpcRepository keeps the `CREATE TABLE IF NOT EXISTS` guard to avoid breaking existing DBs |

### DatabaseService.cs Changes
- Register: `Classes`, `Abilities`, `PlayerCharacters`, `Subspecies`
- RunMigrations() order: Classes → Subclasses (inside ClassRepository) → Species → Subspecies → Abilities → PlayerCharacters
- SeedAllCampaigns(): call `Classes.SeedDefaults()`, `Species.SeedDefaults()`, `Abilities.SeedDefaults()`

---

## UI — System Panel

### S1 — Navbar: Notes + System Buttons

**`NavBar.cs` + `nav_bar.tscn`:**
- Add two flat toggle buttons: `Notes` and `System`
- Active button gets a highlighted style (same Hover color used by tabs)
- Buttons emit a signal or call a method on the parent to switch panels
- Default panel on campaign open: Notes

**`App.cs` or `CampaignDashboard`'s parent:**
- Hold references to both `CampaignDashboard` (Notes) and `SystemPanel` nodes
- On Notes/System toggle: set `Visible` accordingly

### S2 — SystemPanel Shell

**`Scenes/Panels/SystemPanel/SystemPanel.cs` + `system_panel.tscn`:**
- Identical layout to CampaignDashboard (ScrollContainer sidebar + detail pane + reference panel area)
- Sidebar sections: Classes (with nested Subclasses), Species (with nested Subspecies), Abilities
- Same tab system wired in (reuses the same tab infrastructure from CampaignDashboard)
- `SetCampaign(int campaignId)` method mirroring CampaignDashboard

### S3 — Ability Detail Pane

**`Scenes/Components/AbilityDetailPane/AbilityDetailPane.cs` + `.tscn`:**
- Name (large, NameRow + delete button)
- Type (LineEdit: Class Feature / Feat / Racial / etc.)
- Action (OptionButton: Action / Bonus Action / Reaction / Passive / Free / —)
- Trigger (LineEdit)
- Cost (LineEdit)
- Uses (LineEdit, 0 = unlimited)
- Recovery (LineEdit: Short Rest / Long Rest / etc.)
- Effect (TextEdit, multi-line)
- WikiNotes
- Reference Panel: ImageCarousel + "Used By" section (EntityRows linking to Classes/Subclasses/Species)
- Signals: NavigateTo, NameChanged, Deleted

### S4 — Class + Subclass Detail Panes

**`Scenes/Components/ClassDetailPane/ClassDetailPane.cs` + `.tscn`:**
- Name, Description, WikiNotes
- Subclasses section: list of EntityRows (inline add, navigate-to on click)
- Abilities section: EntityRows linking to Ability entities (add/remove)
- Reference Panel: ImageCarousel
- Signals: NavigateTo, NameChanged, Deleted, SubclassAdded

**`Scenes/Components/SubclassDetailPane/SubclassDetailPane.cs` + `.tscn`:**
- Name, Description, Parent Class (label + NavigateTo link)
- Abilities section: EntityRows
- WikiNotes
- Reference Panel: ImageCarousel
- Signals: NavigateTo, NameChanged, Deleted

**Sidebar behavior:** Classes have a ▶/▼ toggle; subclasses appear indented beneath when expanded (same tree pattern as Locations → Sub-locations). Clicking a subclass row in the sidebar opens SubclassDetailPane. The "Add +" button for new subclasses lives inside the ClassDetailPane's Subclasses section, not the sidebar.

### S5 — Species + Subspecies Detail Panes

**`Scenes/Components/SpeciesDetailPane/SpeciesDetailPane.cs` + `.tscn`:**
- Name, Description, WikiNotes
- Subspecies section: EntityRows (inline add, navigate-to)
- Abilities section: EntityRows linking to Ability entities
- Reference Panel: ImageCarousel
- Signals: NavigateTo, NameChanged, Deleted, SubspeciesAdded

**`Scenes/Components/SubspeciesDetailPane/SubspeciesDetailPane.cs` + `.tscn`:**
- Name, Description, Parent Species (label + NavigateTo link)
- Abilities section: EntityRows
- WikiNotes
- Reference Panel: ImageCarousel
- Signals: NavigateTo, NameChanged, Deleted

**Species migration note:** The existing `species` TypeOptionButton on NpcDetailPane continues to work — it still pulls `(id, name)` from the same table. No change to the NPC pane.

### S7 — Player Character Detail Pane (Notes)

**`Scenes/Components/PlayerCharacterDetailPane/PlayerCharacterDetailPane.cs` + `.tscn`:**
- Name (large, NameRow + delete)
- Species TypeOptionButton → `_db.Species.GetAll(campaignId)` (same as NPC, unchanged)
- Subspecies TypeOptionButton → filters by selected SpeciesId (updates when Species changes)
- Class TypeOptionButton → `_db.Classes.GetAll(campaignId)`
- Subclass TypeOptionButton → filters by selected ClassId (updates when Class changes)
- Level LineEdit
- Description TextEdit
- WikiNotes
- **Abilities section**: auto-populated from PC's class + subclass + species + subspecies ability links; each row shows name, action tag, uses counter [−][X/Y][+][↺ reset]; expand row for full ability detail
- Manual ability add (Feats, etc. — adds a `character_abilities` row not sourced from class/species)
- Reference Panel: ImageCarousel

**CampaignDashboard.cs additions:**
- Players accordion in sidebar
- `LoadPlayers()`, `_addPlayersButton`, `_playersContainer`
- `InstantiatePane("player_character", id)` case
- `OnEntityDeleted`, `OnNameChanged`, `FilterSidebar`, `GetEntityColor` cases

### S8 — PC Ability Propagation

When a PC's Class, Subclass, Species, or Subspecies changes:
1. Re-compute the full ability set: union of `class_abilities`, `subclass_abilities`, `species_abilities`, `subspecies_abilities` for the PC's current assignments
2. For each ability in the set: INSERT OR IGNORE into `character_abilities` (preserves existing `uses_remaining`)
3. Remove from `character_abilities` any auto-sourced abilities no longer in the set (but NOT manually added ones)

**Manual abilities:** A `character_abilities` row with `source = 'manual'` vs `source = 'auto'` distinguishes propagated vs user-added. Add a `source TEXT NOT NULL DEFAULT 'auto'` column to `character_abilities`.

---

## Seeding

All seeds use `INSERT OR IGNORE` / `INSERT WHERE NOT EXISTS` — fully idempotent, safe to run on existing campaigns.

### Seed scope (D&D 5e 2024)

| Entity | Examples |
|--------|---------|
| Classes | Fighter, Wizard, Rogue, Cleric, Paladin, Ranger, Druid, Barbarian, Bard, Monk, Sorcerer, Warlock |
| Subclasses | Champion (Fighter), Evocation (Wizard), Thief (Rogue), Life (Cleric), Devotion (Paladin), Hunter (Ranger), Land (Druid), Berserker (Barbarian), Lore (Bard), Open Hand (Monk), Wild Magic (Sorcerer), Fiend (Warlock) |
| Species | Human, Elf, Dwarf, Halfling, Gnome, Half-Orc, Tiefling, Dragonborn, Aasimar, Goliath |
| Subspecies | High Elf / Wood Elf (Elf), Hill Dwarf / Mountain Dwarf (Dwarf), Lightfoot / Stout (Halfling), Forest / Rock (Gnome), Chromatic / Metallic / Gem (Dragonborn) |
| Abilities | Core class features per class (Action Surge, Second Wind, Sneak Attack, Bardic Inspiration, Rage, etc.), common Feats (Alert, Lucky, Tough, War Caster, etc.), racial traits (Darkvision, Fey Ancestry, Stonecunning, etc.) |

Full ability seed list to be defined when S3 is implemented — prioritise the most-used abilities first.

---

## Implementation Order

### Phase 1 — Data Foundation
1. **S11** — Species additive migration (description, notes columns) — low-risk, unblocks S5
2. **S3 data** — `abilities` table + `AbilityRepository` (CRUD only, no link tables yet)
3. **S4 data** — `classes` + `subclasses` tables + `ClassRepository`
4. **S5 data** — `subspecies` table + `SubspeciesRepository`
5. **S6** — All four ability link tables + methods on repositories
6. **S7 data** — `player_characters` additive columns (class_id, subclass_id, subspecies_id, level) + `PlayerCharacterRepository`
7. **S8 data** — `character_abilities` table with `source` column

### Phase 2 — System Panel UI
8. **S1** — Navbar Notes/System toggle buttons
9. **S2** — SystemPanel shell scene (sidebar + detail pane structure)
10. **S3 UI** — AbilityDetailPane + wire into SystemPanel
11. **S4 UI** — ClassDetailPane + SubclassDetailPane + wire into SystemPanel
12. **S5 UI** — SpeciesDetailPane + SubspeciesDetailPane + wire into SystemPanel

### Phase 3 — Notes Integration
13. **S7 UI** — PlayerCharacterDetailPane + wire into CampaignDashboard (Players accordion)
14. **S8 UI** — PC ability propagation logic + use tracking in PlayerCharacterDetailPane

### Phase 4 — Polish
15. **S9** — Full D&D 5e seed data
16. **S10** — Reference Panel rename throughout codebase

---

## File Change Summary

| File | Change | Task |
|------|--------|------|
| `Core/Models/Ability.cs` | **New** | S3 |
| `Core/Models/Class.cs` | **New** | S4 |
| `Core/Models/Subclass.cs` | **New** | S4 |
| `Core/Models/Subspecies.cs` | **New** | S5 |
| `Core/Models/CharacterAbility.cs` | **New** | S8 |
| `Core/Models/Species.cs` | Add Description, Notes | S5 |
| `Core/Models/PlayerCharacter.cs` | Add ClassId?, SubclassId?, SubspeciesId?, Level, Abilities list | S7 |
| `Core/Models/EntityType.cs` | Add PlayerCharacter=6, Class=7, Species=8, Ability=9 | S3 |
| `Core/Repositories/AbilityRepository.cs` | **New** — abilities CRUD + all ability link tables | S3, S6 |
| `Core/Repositories/ClassRepository.cs` | **New** — classes + subclasses CRUD | S4 |
| `Core/Repositories/SubspeciesRepository.cs` | **New** — subspecies CRUD | S5 |
| `Core/Repositories/PlayerCharacterRepository.cs` | **New** — PC CRUD; additive migrations for new columns | S7 |
| `Core/Repositories/SpeciesRepository.cs` | Add description/notes to Migrate() + Map() + Edit() | S5 |
| `Core/DatabaseService.cs` | Register 4 new repos; RunMigrations() + SeedAllCampaigns() | S3–S7 |
| `Scenes/Panels/SystemPanel/SystemPanel.cs` + `.tscn` | **New** — full sidebar + detail pane + tab system | S2 |
| `Scenes/Components/AbilityDetailPane/AbilityDetailPane.cs` + `.tscn` | **New** | S3 |
| `Scenes/Components/ClassDetailPane/ClassDetailPane.cs` + `.tscn` | **New** | S4 |
| `Scenes/Components/SubclassDetailPane/SubclassDetailPane.cs` + `.tscn` | **New** | S4 |
| `Scenes/Components/SpeciesDetailPane/SpeciesDetailPane.cs` + `.tscn` | **New** | S5 |
| `Scenes/Components/SubspeciesDetailPane/SubspeciesDetailPane.cs` + `.tscn` | **New** | S5 |
| `Scenes/Components/PlayerCharacterDetailPane/PlayerCharacterDetailPane.cs` + `.tscn` | **New** | S7 |
| `Scenes/Components/NavBar/NavBar.cs` + `nav_bar.tscn` | Add Notes/System toggle buttons | S1 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Add Players accordion, LoadPlayers(), InstantiatePane case, etc. | S7 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | Add PlayersPanel + ext_resource for PC pane | S7 |

---

## Decisions Log

| # | Question | Decision |
|---|----------|----------|
| Q1 | Ability links on Class/Species panes | **Navigate-to EntityRows** — click opens AbilityDetailPane |
| Q2 | PC manual ability add | **TypeOptionButton** — picks from the System ability list (same dropdown pattern used throughout the app) |
| Q3 | PC class change — ability removal | **Warn first** — "This will remove N abilities from this character's sheet. Continue?" before removing auto-sourced abilities |
| Q4 | Subclasses in sidebar vs. detail pane | **Both** — sidebar shows subclasses nested/indented under their parent class (tree, like sub-locations). Class detail pane also has a Subclasses section with inline add (name + short description) and per-row delete. SubclassDetailPane retains its own delete button. |
| Q5 | Third column name | **Reference Panel** confirmed. Internal node/variable name: `_refPanel` / `ReferencePanel` |

### Q4 Detail — Subclasses in ClassDetailPane

The Subclasses section in ClassDetailPane shows:
- A header row: "Subclasses" label + "Add +" button
- Each subclass as a row: `[Name LineEdit]  [Description LineEdit]  [→ open]  [× delete]`
- Clicking `→` navigates to the full SubclassDetailPane
- "Add +" creates a new subclass row inline and opens its detail pane
- SubclassDetailPane has its own trashcan delete in the NameRow (same pattern as all other panes)
- Sidebar tree: class row has ▶/▼ toggle; subclasses appear indented beneath when expanded

---

*March 2026 · System Layer v1.0 · Living Document*
