# TTRPG Companion App — Project Summary
*March 2026 · v2.0 · Replaces Project_Summary.md + Project_Standards.md*

---

## What This App Is

A desktop TTRPG companion application built in **Godot 4 / C#** with **SQLite** persistence. Designed to replace an Obsidian-based campaign management workflow. The app is system-agnostic at its core, with system-specific modules layered on top — starting with D&D 5.5e (2024).

**GitHub:** `github.com/SeoulP/DnD-Note-Builder`
**Distribution:** GitHub Releases. Exported builds require the `_Data` assemblies folder alongside the `.exe`.

---

## Tech Stack

| Component | Details |
|-----------|---------|
| Engine | Godot 4 — C# (.NET 8) |
| Database | SQLite via `Microsoft.Data.Sqlite` (NuGet) |
| Namespace | `DndBuilder.Core` |
| Persistence | Single `.db` file — portable by design |
| Platforms | Windows, macOS, Linux (Godot export) |

---

## Architecture

### Top-Level Hierarchy

Everything hangs off a **Campaign**. All entities belong to a campaign via `campaign_id` FK.

- Campaign
  - Characters (PCs and NPCs, Table-Per-Type)
  - Sessions
  - Locations
  - Factions
  - Items
  - Quests

### Panel Structure

Two top-level panels, toggled via the NavBar:

- **Notes panel** (`CampaignDashboard`) — NPCs, Locations, Factions, Sessions, Items, Quests, Player Characters
- **System panel** (`SystemPanel`) — Classes, Species, Abilities (campaign-scoped ruleset data)

Both panels share the same three-column layout: Sidebar / Detail Pane / Reference Panel.

### Table Naming Conventions

| Prefix | Scope |
|--------|-------|
| *(none)* | System-agnostic core (`campaigns`, `characters`, `locations`, …) |
| `dnd5e_` | D&D 5.5e module (`dnd5e_skills`, `dnd5e_backgrounds`, …) |
| `pathfinder_` | Pathfinder module (future) |

---

## Hard Rules

These are non-negotiable. Read them before touching any code.

### Schema Is Frozen for Tables With Existing Data

All changes must be **additive only**: new columns with `DEFAULT` values, new join tables, new tables. Never drop columns, rename columns, change types, or drop tables.

### Coding Conventions

- **Synchronous** methods only (no async)
- **Constructor-injected** `SqliteConnection`
- **`@param`-style** parameter binding
- **Ordinal-based** `Map()` methods
- **`Migrate()`** method on every repository (table creation + additive column guards)
- **`Add` / `Edit` / `Delete` / `Get` / `GetAll`** method naming
- **Migration order** in `DatabaseService` ensures FK target tables exist before dependent tables

### Types Are Seeded, Not Hardcoded

All "type" fields (relationship types, statuses, roles, etc.) use per-campaign seeded models. Users can add, rename, and delete entries. Follow the existing `Species` / `NpcRelationshipType` pattern.

### No Destructive UI Assumptions

The schema is the source of truth. UI must tolerate null FKs and missing optional data gracefully.

---

## Agents Folder Structure

All planning and reference documents live in `Agents/`. The convention is:

| File / Folder | Purpose |
|---------------|---------|
| `Project_Summary.md` | This file — overview, rules, pointers |
| `Master_Action_Plan.md` | All open tasks. Points to sub-plans and completed log. |
| `Completed_Action_Plan.md` | Append-only log of all completed work. |
| `PC_Detail_Pane_Action_Plan.md` | Active sub-plan for PC Detail Pane QOL work. |
| `Memory/` | Short reference docs for specific systems — read before touching that system. |

**Convention:** When a task in `Master_Action_Plan.md` is completed, move its full entry to `Completed_Action_Plan.md`. Do not delete it. The completed log is a permanent record.

---

## Memory — System Reference Docs

Before working on any of these systems, read the corresponding doc in `Agents/Memory/`. Each doc explains the design intent, key files, and gotchas that aren't obvious from the code alone.

| System | Doc | What it covers |
|--------|-----|----------------|
| **Theming** | `Memory/theme_manager.md` | Palette structure, `ApplyTheme`, which nodes need manual `ThemeChanged` wiring vs. automatic updates, persistence. Read before touching any colour, style, or theme code. |
| **Tab system** | `Memory/tab_system.md` | Navigation priority rules, pin/close behaviour, drag-to-reorder, save/restore per campaign. Read before touching `CampaignDashboard` tab logic. |
| **WikiLink system** | `Memory/wikilink_system.md` | Parser, autocomplete, stub creation via `[[+Type]]`, alias resolution (P3). Read before touching `WikiNotes`, `WikiLinkParser`, or any entity that emits `NavigateTo`. |

---

*March 2026 · TTRPG Companion App · Project Summary v2.0*
