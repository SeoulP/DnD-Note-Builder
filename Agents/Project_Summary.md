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
| **Engine** | Godot 4 — C# (.NET 8) |
| **Database** | SQLite via Microsoft.Data.Sqlite (NuGet) |
| **Portability** | Single .db file — copy between machines; Dropbox/Drive sync possible later at no extra cost |
| **Target Platforms** | Windows, macOS, Linux (Godot export) |

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

### Project Structure

```
GodotApp/Modules/Notes/
GodotApp/Modules/Characters/Core/
GodotApp/Modules/Characters/Sheets/DnD5e/
GodotApp/Data/Database.cs (SQLite layer)
GodotApp/UI/Shared/ (navigation, search, linking)
```

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
| **Phase 1** | Data Foundation | SQLite schema design, CRUD layer, campaign model |
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

## SQLite Design Notes

To be expanded when development begins. Key points to remember:

- `campaigns` table is the top-level foreign key for everything else — design this in from day one
- Ability use tracking needs: `uses` (max), `uses_remaining`, `recovery_type` (short/long rest), `cost`
- A `references` table handles cross-linking between any two entities
- The `.db` file is the entire save — portable by design
- Cloud sync can be added later by syncing the `.db` file; no app code changes needed

---

*Generated March 2026 • Reference document for project kickoff*
