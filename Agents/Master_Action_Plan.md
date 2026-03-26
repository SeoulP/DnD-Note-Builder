# TTRPG Companion App — Master Action Plan
*March 2026 · v1.2 · Active tasks only — see Completed_Action_Plan.md for completed work**

---

## Guiding Rules

> **No destructive migrations.** The schema is frozen for tables with existing data. All changes must be additive: new columns with defaults, new join tables, new tables only.
>
> **Types are seeded, not hardcoded.** All "type" fields use per-campaign seeded models, not enums. Users can add, rename, and delete entries.
>
> **Fix before building.** All bugs and compile errors are resolved before new features.

---

## Status Key

| Symbol | Meaning |
|--------|---------|
| ✅ | Complete |
| ⬜ | To do |
| 🔶 | Partial / in progress |
| 🚫 | Parked / no decision yet |

---

## Sub-Plans

Large active workstreams have their own dedicated plan files. These are linked here and should be kept in sync with the task entries below.

| File | Scope |
|------|-------|
| [`PC_Detail_Pane_Action_Plan.md`](PC_Detail_Pane_Action_Plan.md) | All PC Detail Pane QOL work — layout restructure, aliases, backgrounds, species resources, rest buttons, skill chips, scaling usages |

---

## Open Task Summary

*Completed tasks are summarised in the [Completed Work Log](#completed-work-log) at the bottom.*

| # | Task | Category | Priority | Status |
|---|------|----------|----------|--------|
| U1 | WikiNotes scroll-jump on first click to edit | UX | High | ✅ |
| U2 | EntityRow hover — show pointer cursor | UX | Low | ✅ |
| U3 | Background colour — project-wide Godot Theme | UX | Low | ✅ |
| U4 | TypeOptionButton — auto-select newly added type | UX | Medium | ✅ |
| U5 | WikiNotes — bullet point continuation on Enter | UX | Low | ✅ |
| F1 | Standardize image save location to `imgs/` folder | Feature | High | ✅ |
| F2 | Per-tab back/forward navigation | Feature | Medium | ✅ |
| F3 | Session related-links panel (wiki panel, third column) | Feature | Medium | ✅ |
| F4 | NPC detail pane — full field audit (HomeLocationId, FirstSeenSession, Personality) | Feature | Medium | 🔶 |
| F5 | Campaign Settings screen — manage seeded types | Feature | Medium | ⬜ |
| F6 | Nested locations in sidebar | Feature | Low | ✅ |
| F7 | Campaign cover image | Feature | Low | ⬜ |
| F8 | Players section — party overview | Feature | Medium | ⬜ |
| F9 | PC abilities / class features | Feature | High | ⬜ |
| F10 | Image export/import in `.dndx` packages | Feature | Medium | ✅ |
| F11 | NPC–Location relationship | Design | Medium | 🚫 |
| F12 | Session detail pane — significant redesign | Design | High | 🔶 |
| F13 | Tab system for the detail pane | Design | Planned | ✅ |
| F14 | Remember last opened entity per campaign | Feature | Medium | ✅ |
| F15 | NPC–NPC relationships — directionality display | Feature | Medium | ✅ |
| F16 | Call SeedDefaults on campaign load (+ add God/Worship seeds) | Schema | Medium | ⬜ |
| F17 | WikiNotes — Items and Quests included in WikiLink autocomplete | Feature | High | ✅ |
| F18 | WikiNotes — stub creation via `[[+NoteType]]` syntax | Feature | High | ✅ |
| M1 | Agents folder cleanup — delete stale files | Maintenance | Low | ⬜ |

---

## Bugs

*No open bugs.*

---

## UX Polish

### U1 — WikiNotes Scroll-Jump Fix ✅

*(See Completed Work Log)*

---

### U2 — EntityRow Pointer Cursor ✅

*(See Completed Work Log)*

---

### U3 — Background Colour: Project-Wide Godot Theme ✅

*(See Completed Work Log)*

---

### U4 — TypeOptionButton: Auto-Select Newly Added Type ✅

*(See Completed Work Log)*

---

### U5 — WikiNotes: Bullet Point Continuation ✅

*(See Completed Work Log)*

---

## Features

### F1 — Standardize Image Save Location to `imgs/` Folder ✅

*(See Completed Work Log)*

---

### F2 — Per-Tab Back/Forward Navigation

**New file: `Scenes/Components/TabHistory.cs`**
```csharp
public class TabHistory
{
    private readonly List<(string EntityType, int EntityId)> _stack = new();
    private int _index = -1;

    public bool CanGoBack    => _index > 0;
    public bool CanGoForward => _index < _stack.Count - 1;
    public (string EntityType, int EntityId) Current => _stack[_index];

    public void Push(string entityType, int entityId)
    {
        if (_index >= 0 && _stack[_index] == (entityType, entityId)) return;
        _stack.RemoveRange(_index + 1, _stack.Count - _index - 1);
        _stack.Add((entityType, entityId));
        _index++;
    }

    public (string EntityType, int EntityId) Back()    { if (CanGoBack)    _index--; return Current; }
    public (string EntityType, int EntityId) Forward() { if (CanGoForward) _index++; return Current; }
}
```

**`CampaignDashboard.cs` changes:**
- Add `private TabHistory _history = new();` (non-readonly — needs reset on campaign switch).
- Add `[Export] private Button _backButton` and `[Export] private Button _forwardButton`.
- Split `ShowDetailPane` into `NavigateToInternal(string entityType, int entityId, bool pushHistory)`.
  - `pushHistory: true` → `_history.Push(...)` then `RefreshNavButtons()`.
  - `pushHistory: false` → skip push (used by back/forward traversal).
- `_backButton.Pressed` → `NavigateToInternal(_history.Back(), pushHistory: false)`.
- `_forwardButton.Pressed` → `NavigateToInternal(_history.Forward(), pushHistory: false)`.
- `RefreshNavButtons()` sets `Disabled` from `_history.CanGoBack` / `CanGoForward`.
- Reset `_history` in `SetCampaign()`.

**Mouse side-buttons — `_UnhandledInput` in `CampaignDashboard`:**
```csharp
public override void _UnhandledInput(InputEvent e)
{
    if (e is InputEventMouseButton { Pressed: true } mb)
    {
        if (mb.ButtonIndex == MouseButton.Xbutton1 && _history.CanGoBack)
        { NavigateToInternal(_history.Back(), pushHistory: false); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Xbutton2 && _history.CanGoForward)
        { NavigateToInternal(_history.Forward(), pushHistory: false); AcceptEvent(); }
    }
}
```

**`CampaignDashboard.tscn`:** Add a small `HBoxContainer` with `←` and `→` flat buttons above or inset into `_detailPanel`. Wire exports.

---

### F3 — Session Related-Links Panel ✅

*(See Completed Work Log)*

---

### F4 — NPC Detail Pane: Home Location Field 🔶 Partial

Status and RelationshipTypeId dropdowns are done. Remaining field worth adding:

| Field | Source | Action |
|-------|--------|--------|
| `HomeLocationId` | `Npc` | Add Location `TypeOptionButton` to NPC pane |

`Personality` and `FirstSeenSession` deferred — personality isn't a priority, and first-seen session is better handled as part of the F12 session pane redesign.

---

### F5 — Bulk Type Management Screen

A dedicated screen for managing all per-campaign seeded types in one place — useful for pre-session prep without having to open individual records. The inline `TypeOptionButton` already handles add/delete on the fly; this is purely a convenience screen for bulk editing.

**Recommended approach:** One `CampaignSettingsModal` scene with a `TabContainer`. Each tab is an instance of a shared `TypeEditorPanel` subscene (list + name input + Add/Delete buttons). Each tab wired to a different repository.

**Tables to manage:**

| Tab | Repository | Table |
|-----|------------|-------|
| Species | `SpeciesRepository` | `species` |
| NPC Statuses | `NpcStatusRepository` | `npc_statuses` |
| NPC Relationships | `NpcRelationshipTypeRepository` | `npc_relationship_types` |
| Faction Roles | `NpcFactionRoleRepository` | `npc_faction_roles` |
| Item Types | `ItemTypeRepository` | `item_types` |

> Do not allow deleting a type that any records currently reference — warn the user before deletion.

---

### F6 — Nested Locations in Sidebar ✅

*(See Completed Work Log)*

---

### F7 — Campaign Cover Image

Add `Campaign` to the `EntityType` enum in `EntityType.cs`. Everything else (`EntityImageRepository`, `ImageCarousel`) already works generically for any entity type. No other data layer changes required.

---

### F8 — Players Section: Party Overview

`PlayerCharacter.cs` model exists. Full PC mechanics are deferred to F9.

**Scope:**
- Add a Players accordion to `CampaignDashboard` alongside the existing sections.
- Detail pane shows: name, species, class, level — basic identifying information only.

---

### F9 — PC Abilities / Class Features

**New `abilities` table:**

| Column | Type | Notes |
|--------|------|-------|
| `id` | INTEGER PK | |
| `player_character_id` | INTEGER FK | → `player_characters(id)` |
| `name` | TEXT | |
| `type` | TEXT | Class Feature, Maneuver, Feat, etc. |
| `action` | TEXT | Action / Bonus Action / Reaction / Passive / Free |
| `trigger` | TEXT | Optional — e.g. "When you miss an attack" |
| `cost` | TEXT | Optional — e.g. "1 Superiority Die" |
| `uses` | INTEGER | Max uses (0 = unlimited) |
| `uses_remaining` | INTEGER | Current remaining uses |
| `recovery` | TEXT | e.g. "Short Rest", "+1 per Short Rest" |
| `effect` | TEXT | Full description |
| `notes` | TEXT | Rulings and reminders |
| `sort_order` | INTEGER | Display order within character sheet |

---

### F10 — Image Export/Import in `.dndx` Packages ✅

*(See Completed Work Log)*

---

### F11 — NPC–Location Relationship 🚫 No Decision

A single location field on an NPC conflates three distinct concepts:

| Concept | Description |
|---------|-------------|
| Home / Origin | Where they are from or based. Stable, set during prep. |
| Current Location | Where they are right now. Changes during play. |
| Last Seen | Session-stamped observation — "As of Session 12, spotted in Waterdeep." |

**Options under consideration:**
1. Two optional fields on `Npc`: `HomeLocationId` (FK already exists) + `LastSeenNote` (freetext with optional session reference).
2. A join table `npc_locations` with `location_id`, `session_id`, `note` — a full history.

> Option 1 is simpler and covers most DM use cases. Option 2 is more powerful but adds complexity before the need is proven. No decision yet — revisit when building the NPC pane update (F4).

---

### F12 — Session Detail Pane: Significant Redesign 🔶 In Progress

The current pane has: number label, title, played-on date, wiki notes, image carousel, delete. Most-used pane during active play; confirmed as the biggest active pain point from live session use.

**Confirmed missing (from live session feedback):**
- Abilities panel — quick reference during combat
- Quick-create for entities from within the session (tracked as F18 — see below)
- WikiLink coverage gap — Items and Quests not linked (tracked as F17)
- Bullet point support in notes (tracked as U5)

**Previously planned — still applies:**
- Entity tagging panel — structured fields for which NPCs, locations, factions, and quests appeared
- Inline stub creation — create a new NPC or location without losing place
- Wiki link hover preview — hovering a `[[Link]]` shows a preview without navigating
- Quest status updates — log progress on a quest from within session notes

> Plan together with F13 (Tab system) — they are the same body of work.

---

### F13 — Tab System for the Detail Pane ✅

*(See Completed Work Log)*

---

### F14 — Remember Last Opened Entity Per Campaign ✅

*(See Completed Work Log — superseded by full tab restoration via "Remember Tabs" setting)*

---

### F15 — NPC–NPC Relationships: Directionality Display ✅

*(See Completed Work Log)*

---

### F16 — Call SeedDefaults on Campaign Load

All `SeedDefaults` implementations already use `INSERT ... WHERE NOT EXISTS (... AND name = @name)` — they are fully idempotent and safe to call repeatedly. The only gap is they are currently only called from `DatabaseService` at campaign *creation*, not on subsequent loads.

**Fix — `DatabaseService.cs` or `CampaignDashboard.SetCampaign()`:** Call all `SeedDefaults` on campaign load, not just on creation. Because all the INSERT guards are already in place, this is safe for existing campaigns with real data.

```csharp
// Call in SetCampaign or wherever a campaign is opened:
_db.Species.SeedDefaults(campaignId);
_db.LocationFactionRoles.SeedDefaults(campaignId);
_db.NpcFactionRoles.SeedDefaults(campaignId);
_db.NpcRelationshipTypes.SeedDefaults(campaignId);
_db.NpcStatuses.SeedDefaults(campaignId);
_db.FactionRelationshipTypes.SeedDefaults(campaignId);
_db.CharacterRelationshipTypes.SeedDefaults(campaignId);
_db.ItemTypes.SeedDefaults(campaignId);
_db.QuestStatuses.SeedDefaults(campaignId);
```

This also means any new seeds added to a `Defaults` array in the future will automatically appear in existing campaigns on next open — making F16 the permanent solution, not a one-time patch.

**New seeds to add at the same time:**

| Repository | New entry |
|------------|-----------|
| `CharacterRelationshipTypeRepository` | `("Worships", "Reveres as a deity or divine figure")` |
| `NpcFactionRoleRepository` | `("Worshipped by", "Venerated as a god or divine patron by this faction")` |

These will reach existing campaigns automatically once F16 is wired in.

---

### F17 — WikiNotes: Items and Quests in WikiLink Autocomplete ✅

*(See Completed Work Log)*

---

### F18 — WikiNotes: Stub Creation via `[[+NoteType]]` Syntax ✅

*(See Completed Work Log)*

---

### M1 — Agents Folder Cleanup ⬜

Delete the following stale files and folders from the `Agents/` directory. All content has either been completed (logged in `Completed_Action_Plan.md`), superseded, or absorbed into active plans.

**Files to delete:**
- `WAL_Urgent_Action_Plan.md` — complete, wired in codebase
- `AppLogger_Action_Plan.md` — complete, `AppLogger.cs` + `Toast` exist
- `Abilities_Refactor_Action_Plan.md` — fully superseded by current architecture
- `System_Layer_Action_Plan.md` — fully superseded; all work in completed log
- `party_character_skills_action_plan.md` — absorbed into completed log

**Folders to delete:**
- `Agents/Graush/` — temporary ability reference docs used during seeding; data now in DB
- `Agents/Classes/` — temporary class reference docs; data now in DB
- `Agents/Species/` — temporary species reference docs; data now in DB
- `memory/` (project root) — replaced by `Agents/Memory/`

---

## Implementation Order

### Short-term
1. **F16 — Call SeedDefaults on campaign load** — trivial wiring; ships the God/Worship seeds too.
2. **F4 — NPC pane: Home Location field** — small addition, finish the partial work.

### Medium-term
3. **F5 — Bulk type management screen** — convenience, not blocking anything.
4. **F8 — Players section** — basic party overview.

### Planned / Larger scope
5. **F9 — PC abilities / class features** — new table, greenfield.
6. **F12 — Session redesign** — entity tagging, inline stub creation, wiki hover preview.
7. **F7 — Campaign cover image** — one enum entry; trivial but low priority.

### Blocked / No decision
- **F11 — NPC–Location relationship** — revisit later; design TBD.

---

## File Change Summary

| File | Change | Task |
|------|--------|------|
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Magic-byte format detection + raw byte loading; OS drop via `Window.FilesDropped` | B1, B2 |
| `Scenes/Components/ImageLightbox/ImageLightbox.cs` | Magic-byte format detection + raw byte loading | B1 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Fix `_renderer.GuiInput` handler; add `RestoreScroll()` | U1 |
| `Scenes/Components/EntityRow/EntityRow.cs` | Add `MouseDefaultCursorShape = CursorShape.PointingHand` in `_Ready()` | U2 |
| `theme.tres` | **New** — project-wide Godot theme: TextEdit/LineEdit styles, NavBar, hover states, popup bg | U3 |
| `project.godot` | `[gui] theme/custom`, `[rendering] environment/defaults/default_clear_color` | U3 |
| `Scenes/Components/NavBar/nav_bar.tscn` | Inline StyleBoxFlat panel override — background `#0f172a` | U3 |
| `Scenes/Components/EntityRow/EntityRow.cs` | Hover boxes read from theme with hardcoded fallback | U3 |
| `Scenes/Components/TypeOptionButton/TypeOptionButton.cs` | Hover boxes from theme; popup bg from theme; `CaretBlink = true` on dynamic LineEdits | U3 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | Background `#1e293b`, border `#334155` to match app palette | U3 |
| `Scenes/Components/*/[detail pane].tscn` (×5) | `margin_top = 8` restored on `Margin` node | U3 |
| `Scenes/Components/*/[detail pane].tscn` (all) | `caret_blink = true` on all LineEdit and TextEdit nodes | U3 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | `caret_blink = true` on SearchInput | U3 |
| `Scenes/Modals/NewCampaignModal/add_campaign_modal.tscn` | `caret_blink = true` on NameLineEdit and DescriptionTextEdit | U3 |
| `Scenes/App.cs` | `_Input` override — release focus when clicking outside active control | U3 |
| `NpcDetailPane.cs`, `ItemDetailPane.cs`, `QuestDetailPane.cs`, `FactionDetailPane.cs`, `LocationDetailPane.cs` | Add `AutoSelectOnAdd = true` before each type dropdown `Setup()` call | U4 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add `HandleBulletContinuation()`; extend `OnInputKey` Enter handler; relax early-return guard | U5 |
| `Core/DatabaseService.cs` | Added `ImgDir` property; `_Ready()` uses exe dir for exported builds; added `Disconnect()` method | F1 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | `CopyToImgDir()` copies to `img/{campaignName}/` with GUID name; `DeleteCurrentImage()` removes file from disk; `Setup()` accepts `campaignId`; `_campaignId` field | F1 |
| `Core/Models/EntityImageExport.cs` | **New** — `EntityImageExport` model for base64-embedded images in export packages | F10 |
| `Core/Models/ExportPackage.cs` | Added `List<EntityImageExport> Images` | F10 |
| `Core/ImportExportService.cs` | `BuildPackage()` gathers + base64-encodes images; `ApplyPackage()` Step 8 writes images, added `itemMap`/`questMap`; `SanitizeFolderName()` helper | F10 |
| `Scenes/Modals/ImportExportModal/ImportExportModal.cs` | `_includeImagesCheckbox` field; Include Images row in `BuildSections()`; `sel.IncludeImages` in `GatherSelection()` | F10 |
| `Scenes/Components/NavBar/NavBar.cs` | `OpenBackupDialog()` replaced with popup Window + Include Images checkbox; WAL-safe zip backup with `Disconnect()`/`Reconnect()`; `-wal`/`-shm` sidecars included; restore accepts `.zip` or `.db`; both modals use `PopupCenteredClamped` | F1, F10 |
| All six detail panes | Pass `entity.CampaignId` as 4th arg to `_imageCarousel.Setup()` | F1 |
| `Core/Repositories/EntityImageRepository.cs` | Added `UpdatePath(int id, string newPath)` | F1 |
| `Core/DatabaseService.cs` | Added `MigrateLegacyImagePaths(campaignId)` + `SanitizeFolderName()` helper; `SqliteConnection.ClearAllPools()` in `Disconnect()` | F1 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | `MigrateLegacyImagePaths` call moved to `_Ready()` (after `_db` is initialized); removed from `SetCampaign()` | F1 |
| `Scenes/Components/ImageCarousel/ImageCarousel.cs` | `CopyToImgDir()` returns relative path; `ResolveToAbsolute()` instance method added; `Refresh()`, `DeleteCurrentImage()`, `OpenLightbox()` all use resolved paths | F1 |
| `Core/ImportExportService.cs` | `ResolveToAbsolute(path, appDir)` static helper; `BuildPackage()` resolves before reading bytes; `ApplyPackage()` Step 8 stores relative path | F10 |
| `Scenes/Components/TabHistory.cs` | **New** — `TabHistory` helper class | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Add history, back/forward buttons, `NavigateToInternal`, `_UnhandledInput`, `RefreshNavButtons()` | F2 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | Add `←` / `→` buttons; wire exports | F2 |
| `Scenes/Components/SessionDetailPane/SessionDetailPane.cs` | Add `RefreshRelatedLinks()`, `BuildLinkSection()`, `StripButtonPadding()`; update `_notes.TextChanged` | F3 |
| `Scenes/Components/SessionDetailPane/session_detail_pane.tscn` | Add `RelatedLinks` VBoxContainer under `CarouselColumn` between `ImageCarousel` and `CarouselSpacer`; wire `_relatedLinksContainer` export | F3 |
| `Scenes/Components/NpcDetailPane/NpcDetailPane.cs` + `.tscn` | Add HomeLocationId, FirstSeenSession, verify Personality | F4 |
| `Core/Repositories/*RelationshipTypeRepository.cs` et al. | Add check-and-insert on campaign load for new seeds | F16 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Add Items + Quests to `GetEntityMatches()`; add `EntityCreated` signal; add `DetectStubTrigger()`, `OpenStubModal()`, `CreateStub()`; extend `CheckAutocomplete()` | F17, F18 |
| `Core/WikiLinkParser.cs` | Add Items + Quests to reverse-lookup for link rendering | F17 |
| All six detail panes | Wire `_notes.EntityCreated` → `EmitSignal(EntityCreated)`; add `EntityCreated` signal declaration where missing | F18 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Wire `EntityCreated` for sesPane, itemPane, questPane | F18 |
| `Scenes/Components/WikiNotes/wiki_notes.tscn` | Add `StubHint` label footnote | F18 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs` | Full tab system: `TabEntry`, `BuildTabWidget`, `BuildAddTabWidget`, `OpenTab`, `CloseTab`, `SwitchTab`, `SaveTabs`, `RestoreTabs`, `OnTabThemeChanged`, drag-and-drop reorder, right-click context menu, `_Input` override for drag; removed `NewTabButton` | F13 |
| `Scenes/Panels/CampaignDashboard/CampaignDashboard.tscn` | Remove `NewTabButton`; `horizontal_scroll_mode = 3` on `TabScroll`; `TabRow` min height 36 | F13 |
| `Core/ThemeManager.cs` | `DeleteHoverColor` changed to dark crimson `Color(0.55f, 0.12f, 0.12f)` | F13 |
| `Scenes/Components/NavBar/NavBar.cs` | "Remember Tabs" checkable menu item (id=5); `HideOnCheckableItemSelection = false`; persisted via `Settings.Set("remember_tabs", ...)` | F13, F14 |
| `Scenes/Components/EntityRow/EntityRow.cs` | `_deleteHoverBox` fallback updated to `ThemeManager.DeleteHoverColor` | F13 |
| `Scenes/Components/TypeOptionButton/TypeOptionButton.cs` | `_deleteHoverBox` fallback updated to `ThemeManager.DeleteHoverColor` | F13 |
| `Scenes/Components/LocationDetailPane/LocationDetailPane.cs` | `LoadParentRow()`: show muted "No Parent" label when `ParentLocationId` is null | F13 |
| `Agents/Project_Standards.md` | Added "Theming Conventions" section (ThemeManager palette, DeleteHoverColor, ThemeChanged pattern, theme.tres styleboxes, hardcoded colour rules) | F13 |

---

## Completed Work Log

All completed tasks have been moved to [`Completed_Action_Plan.md`](Completed_Action_Plan.md).

---

*Generated March 2026 · TTRPG Companion App · Master Action Plan · v1.2*
