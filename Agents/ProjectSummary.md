# TTRPG Companion App
## Project Summary & Architecture Plan
*March 2026 • v1.0*

---

## Project Overview

A desktop TTRPG companion application built in Godot 4 / C# with SQLite persistence. Designed to replace an Obsidian-based workflow that was hitting limitations around structured character data, use tracking, and cross-referencing. The app is system-agnostic at its core, with system-specific character sheet modules layered on top.

---

## Tech Stack

| Component | Details |
|---|---|
| **Engine** | Godot 4.6 — C# (.NET 8) |
| **Database** | SQLite via Microsoft.Data.Sqlite (NuGet) |
| **Portability** | Single .db file — copy between machines; Dropbox/Drive sync possible later at no extra cost |
| **Target Platforms** | Windows, macOS, Linux (Godot export) |

---

## Current Project Structure

This is the actual structure on disk as of Phase 1:

```
dnd-builder/
├── project.godot               # Godot config; autoloads DatabaseService
├── DndBuilder.csproj / .sln
├── Core/
│   ├── DatabaseService.cs      # SQLite singleton — all DB access
│   └── Models/
│       └── Campaign.cs         # Campaign model (namespace: DndBuilder.Core.Models)
├── Scenes/
│   ├── app.tscn                # Root scene (App shell)
│   ├── CampaignList/           # Scrollable list of campaign cards
│   ├── CampaignCard/           # Individual campaign display card
│   └── NewCampaignModal/       # Add campaign modal (Window subclass)
└── Agents/
    ├── ProjectSummary.md       # This file
    └── SceneRefactorActionPlan.md
```

### Target Structure (future phases)

```
Core/
Modules/
├── Notes/
└── Characters/
    ├── Core/
    └── Sheets/DnD5e/
Scenes/
└── Shared/   (navigation, search, linking)
```

---

## Architecture

### Top-Level Hierarchy

Everything in the app hangs off a Campaign. This keeps multiple active campaigns cleanly separated and allows archiving.

- **Campaign (top-level container)**
  - Characters (PCs and NPCs)
  - Sessions (session notes)
  - Locations
  - Factions
  - Notes (freeform, generic)

### Module Separation

The Notes module and the Character module are kept intentionally separate. A shared linking layer connects them so notes can reference characters, NPCs, locations, etc., without coupling the modules directly.

- **Notes Module — system-agnostic, freeform text**
  - Sessions, Locations, Factions, NPC pages
  - Plain text to start; markdown rendering as a later enhancement

- **Characters Module — generic base + system-specific sheets**
  - Core model: name, portrait, campaign reference, notes links
  - DnD5e sheet: extends Core with 5.5e-specific schema
  - Future systems (Pathfinder, etc.) add new sheet types without touching Notes or Core

### Key Conventions

- `DatabaseService` is a Godot autoload at `/root/DatabaseService`. All scenes access it via `GetNode<DatabaseService>("/root/DatabaseService")`.
- Models live in `Core/Models/` under the `DndBuilder.Core.Models` namespace.
- Scenes are paired: a `.tscn` file and a same-named `.cs` script in their own subfolder under `Scenes/`.
- Signals use Godot's `[Signal]` delegate pattern for cross-node communication.
- UI nodes are injected via `[Export]` properties wired in the editor.

---

## DnD 5.5e (2024) Character Sheet — Priority 1

### Sections & Complexity

| Section | Notes | Complexity |
|---|---|---|
| **Core Stats** | STR/DEX/CON/INT/WIS/CHA, saves, skills, proficiency bonus | Low |
| **HP & Defenses** | Current/max HP, Hit Dice, AC, Initiative, Speed | Low |
| **Attacks & Weapons** | Weapon list, attack bonus, damage, properties | Medium |
| **Class Features & Abilities** | Use tracking (uses/uses_remaining), recovery type, action type, cost | High — current pain point |
| **Spell Slots & Spells** | Slots per level, concentration, ritual, prepared vs known | High — Phase 2 |
| **Inventory & Equipment** | Item list, weight, attunement | Medium |
| **Background & Flavor** | Background, bonds, flaws, ideals, appearance | Low |

---

## Build Order

| Phase | Focus | Deliverables |
|---|---|---|
| **Phase 1** | Data Foundation | SQLite schema, CRUD layer, campaign model — **in progress** |
| **Phase 2** | 5.5e Character Sheet | Core stats, HP, abilities with use tracking — solves immediate Obsidian pain |
| **Phase 3** | Notes Module | Session notes, NPC pages, Locations, Factions |
| **Phase 4** | Linking Layer | Cross-references between notes ↔ characters ↔ NPCs |
| **Phase 5** | Spells | Slot tracking, concentration, prepared/known lists |
| **Phase 6+** | Polish & Extras | Markdown rendering, portraits, additional game systems |

---

## Known Challenges

- **Rich text / notes editor** — no built-in markdown editor in Godot; start with plain TextEdit and enhance later
- **Spell management** — 5.5e spell system is complex; intentionally deferred to Phase 5
- **Linking layer** — requires a references table in SQLite and some UI design work; defer to Phase 4

---

## SQLite Schema (Live)

DB file location: `OS.GetUserDataDir()/campaign.db`

### `campaigns`

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER | Primary key |
| `name` | TEXT | Required |
| `system` | TEXT | Default `dnd5e_2024` |
| `description` | TEXT | Optional |
| `date_started` | TEXT | ISO date string (yyyy-MM-dd) |
| `created_at` | TEXT | Auto-set to `datetime('now')` |
| `archived` | INTEGER | 0 = active, 1 = archived |

Supported `system` values: `dnd5e_2024`, `pathfinder2e`

### Future tables (not yet created)

- `characters` — campaign FK, name, system, portrait
- `sessions`, `locations`, `factions` — campaign FK, freeform notes
- `references` — cross-link table between any two entities (id, from_type, from_id, to_type, to_id)
- Ability use tracking fields: `uses`, `uses_remaining`, `recovery_type` (short/long rest), `cost`

---

*Generated March 2026 • Reference document for project kickoff*
