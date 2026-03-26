---
name: WikiLink system
description: How WikiLinks work — parser, autocomplete, stub creation, alias resolution
type: project
---

The WikiLink system spans three files: `Scenes/Components/WikiNotes/WikiNotes.cs`, `Core/WikiLinkParser.cs`, and (upcoming) `Core/Repositories/EntityAliasRepository.cs`.

## Overview

WikiNotes is a dual-mode control: a `RichTextLabel` (read mode) and a `TextEdit` (edit mode). In read mode, `[[EntityName]]` syntax is rendered as clickable gold links that emit `NavigateTo`. In edit mode, typing `[[` triggers an autocomplete dropdown.

## WikiLinkParser

`Core/WikiLinkParser.cs` handles all parsing and rendering. Key method:

**`BuildLookup(int campaignId, DatabaseService db)`** — queries all entity types and returns a `Dictionary<string, (string entityType, int entityId)>` mapping display name → navigation target. Entity types covered: NPCs, Factions, Locations, Sessions, Items, Quests, PlayerCharacters.

**`ParseAndRender(string text, lookup)`** — walks the raw text, finds all `[[...]]` tokens, substitutes resolved ones with BBCode `[url=type:id]Name[/url]` gold links, and leaves unresolved ones as grey muted text.

When adding a new entity type to WikiLink support, update both `BuildLookup()` and any reverse-lookup used for link rendering.

## Autocomplete

`CheckAutocomplete()` in `WikiNotes.cs` fires on every keypress in edit mode. Triggers when the caret is inside an unclosed `[[` token. Queries `GetEntityMatches(prefix)` which searches all entity types by name prefix (case-insensitive). Results populate a `PopupMenu` anchored below the caret.

Selecting an autocomplete result replaces the partial `[[text` with `[[FullEntityName]]` and closes the popup.

**Keys handled while popup is open:** Escape (close), Up/Down (navigate), Tab/Enter (select). These are consumed via `AcceptEvent()` so they don't also move the caret.

## Stub Creation — `[[+NoteType]]` Syntax

Typing `[[+NPC]]`, `[[+Location]]`, `[[+Item]]`, `[[+Faction]]`, or `[[+Quest]]` and closing with `]]` triggers `DetectStubTrigger()`. If a valid type is detected:

1. `OpenStubModal()` shows a name-prompt `ConfirmationDialog`
2. On confirm: `CreateStub(type, name)` creates a minimal record in the DB
3. The trigger text is replaced with `[[Name]]` in the TextEdit
4. `EntityCreated` signal is emitted — `CampaignDashboard` wires this to refresh the sidebar

On Escape: the trigger text is removed entirely.

Supported stub types and their create methods:

| Trigger | Creates | Method |
|---------|---------|--------|
| `[[+NPC]]` | `Npc` | `_db.Npcs.Add(...)` |
| `[[+Location]]` | `Location` | `_db.Locations.Add(...)` |
| `[[+Item]]` | `Item` | `_db.Items.Add(...)` |
| `[[+Faction]]` | `Faction` | `_db.Factions.Add(...)` |
| `[[+Quest]]` | `Quest` | `_db.Quests.Add(...)` |

## Alias Resolution (P3 — not yet built)

When `EntityAliasRepository` is implemented, `BuildLookup()` will be extended to also load all `entity_aliases` rows for the campaign and add each `alias → (entityType, entityId)` mapping to the lookup dictionary.

**Priority:** If an alias conflicts with a real entity name, the entity name takes precedence.

**Display:** Aliases resolve to the entity but the link text stays as typed — `[[Zemmy]]` renders as "Zemmy" and navigates to Zemmouregal.

**Autocomplete:** `GetEntityMatches()` will also search alias text. Alias matches will show as `Zemmy (→ Zemmouregal)` in the dropdown.

## Signals

| Signal | Emitted when | Wired to |
|--------|-------------|----------|
| `NavigateTo(string type, int id)` | User clicks a resolved WikiLink | `CampaignDashboard` → `ShowDetailPane` |
| `TextChanged` | Any edit in the TextEdit | Parent detail pane → `Save()` |
| `EntityCreated(string type, int id)` | Stub created via `[[+Type]]` | `CampaignDashboard` → sidebar refresh |

## Adding a New Entity Type

To add a new entity type to WikiLink support:
1. Add a query in `WikiLinkParser.BuildLookup()` for the new table
2. Add a search branch in `WikiNotes.GetEntityMatches()` 
3. If stub creation is needed, add a case in `DetectStubTrigger()` and `CreateStub()`
4. Wire `EntityCreated` in the new entity's detail pane if stubs are supported
