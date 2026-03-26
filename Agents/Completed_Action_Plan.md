# TTRPG Companion App — Completed Action Plan
*March 2026 · Append-only log of all completed work*

> **Convention:** When a task in `Master_Action_Plan.md` is completed, move its full entry here. Do not delete it. This file is a permanent record.

---

## Model (pre-alpha)
- ✅ `Faction.Headquarters` removed
- ✅ `Location.FactionIds` → `LocationFaction` join model
- ✅ `LocationFactionRole` seeded model created
- ✅ `NpcRelationship` enum → `NpcRelationshipType` seeded model
- ✅ `Npc` refactored into `Character` base + `Npc` + `PlayerCharacter` (Table-Per-Type)
- ✅ `NpcStatus` enum → `NpcStatus` seeded model
- ✅ Item system: `Item`, `ItemType`, `SystemItem`, `DnD5eItemMechanics`, `CharacterItem`, `LocationItem`

---

## UI / Compile fixes (pre-alpha)
- ✅ `FactionDetailPane` — Headquarters removed
- ✅ `LocationDetailPane` — `FactionIds` → `Factions.Select(f => f.FactionId)`
- ✅ `NpcDetailPane` — enum dropdowns replaced with DB-loaded dropdowns (Status + RelationshipType)
- ✅ Item list + detail pane
- ✅ Entity image carousel system (`ImageCarousel`, `ImageLightbox`, `EntityImageRepository`, all five detail panes wired)

---

## WAL / Database (2026-03-19)
- ✅ SQLite WAL mode — `PRAGMA journal_mode=WAL` + `PRAGMA synchronous=NORMAL` added to `DatabaseService.InitConnection()`. Fixes post-write read visibility on all flows (campaign creation, import, entity creation). `-wal`/`-shm` sidecars already handled in backup flow.

---

## AppLogger + Toast (2026-03-19)
- ✅ `Core/AppLogger.cs` — levelled logger (TRACE/DEBUG/INFO/WARNING/ERROR), append-only `app.log`, fires `ToastRequested` event at WARNING+
- ✅ `Scenes/Components/Toast/Toast.cs` + `toast.tscn` — bottom-centre `CanvasLayer` toast with auto-dismiss, severity styling (ERROR=red, WARNING=amber)
- ✅ `App.cs` — subscribes to `AppLogger.Instance.ToastRequested`, instantiates Toast
- ✅ `NavBar.cs` — Log Level selector in settings popup; Clear App Log menu item with `ConfirmationDialog`
- ✅ Existing `try/catch` blocks in `NavBar.cs` and `ImportExportService` migrated to `AppLogger`; `backup_debug.log` one-off removed

---

## UX Polish (2026-03-19)
- ✅ U2 — EntityRow pointer cursor: `MouseDefaultCursorShape = CursorShape.PointingHand` on `navBtn` and `delBtn` inside `EntityRow._Ready()`.
- ✅ U1 — WikiNotes click-to-edit fully resolved: scroll position saved/restored around `GrabFocus`; caret placed at click position via `GetLineColumnAtPos`; `ScrollFitContentHeight = true` prevents scrollbar flicker; fonts/sizes matched between `TextEdit` and `RichTextLabel` via theme API; renderer stylebox tuned to match focused TextEdit layout; `caret_blink = true` added to `wiki_notes.tscn`.

---

## Image loading (2026-03-19)
- ✅ B1 — Image load bug fixed: `ImageCarousel` and `ImageLightbox` now read raw bytes via `System.IO.File.ReadAllBytes` and detect format from magic bytes (PNG/JPEG/WebP), bypassing Godot's extension-based loader. Handles files with mismatched extensions.
- ✅ B2 — Drag-and-drop now works via `Window.FilesDropped` signal. Godot 4 does not route OS file drops through `_CanDropData`/`_DropData`. Drop hover overlay dropped — Godot 4 exposes no OS drag hover events.

---

## Visual / Theme polish (2026-03-20)
- ✅ U3 — Project-wide Godot theme (`theme.tres`): TextEdit + LineEdit steel-blue bg (`#334155`), violet focus ring (`#6d28d9`), rounded corners (r=5), transparent resting border; warm light text.
- ✅ ThemeManager — `Core/ThemeManager.cs` autoload; HSV hue-shift approach; free hue slider + Dark/Light toggle + 4-stop saturation. Persisted via `SettingsRepository` (keys: `theme_hue`, `theme_dark`, `theme_sat`). Special-case: hue=215 + dark + Default sat → exact Slate Tailwind values.
- ✅ App background `#1e293b` via `rendering/environment/defaults/default_clear_color`.
- ✅ NavBar background `#0f172a` via inline StyleBoxFlat override in `nav_bar.tscn`.
- ✅ EntityRow + TypeOptionButton hover states read from theme; hardcoded fallback prevents null-stylebox override bug.
- ✅ ImageCarousel background `#1e293b`, border `#334155`, border width 0.
- ✅ `margin_top = 8` restored on `Margin` node in all five detail panes.
- ✅ `caret_blink = true` added to every LineEdit and TextEdit node across all detail panes, dashboard search, new-campaign modal, and dynamic TypeOptionButton inputs.
- ✅ Global unfocus on blank click — `App._Input` override.

---

## System Layer (2026-03-20)
- ✅ NavBar — Notes + System toggle buttons
- ✅ `SystemPanel` shell scene — sidebar + detail pane + reference panel, tab system wired
- ✅ `Ability` — model, repo, `AbilityDetailPane`; `ability_types`, `ability_resource_types`, `ability_costs`, `ability_class_levels`, `ability_species_levels`, `ability_subclass_levels`, `ability_subspecies_levels`, `character_abilities`, `character_resources` tables
- ✅ `Class` + `Subclass` — model, repo, `ClassDetailPane`, `SubclassDetailPane`; `classes`, `subclasses`, `class_levels` tables; level progression with ability picker + usages field
- ✅ `Species` + `Subspecies` — model, repo, `SpeciesDetailPane`, `SubspeciesDetailPane`; species expanded with description/notes; `subspecies` table
- ✅ Ability link tables — `ability_class_levels`, `ability_species`, `ability_subclasses`, `ability_subspecies`
- ✅ Player Characters — `PlayerCharacterRepository`; `player_characters` additive columns (class_id, subclass_id, subspecies_id, level, ability scores, background_id); `PlayerCharacterDetailPane` with stats, skills, ability choices, resources
- ✅ PC ability propagation — `GetAllOwnedAbilities()` unions class/subclass/species/subspecies levels; `SyncResources()` mirrors resource types to `character_resources`
- ✅ Seeding — D&D 5e classes, subclasses (Fighter+Champion seeded for Graush), species, subspecies (Orc seeded), abilities (full Fighter + Orc ability set seeded)

---

## Session feedback (2026-03-20)
- ✅ Spacebar opens image importer from lightbox — `GetViewport().GuiReleaseFocus()` in `OpenLightbox()`
- ✅ Session sidebar — title only, no index prefix
- ✅ Wasted space left/right of app — `app.tscn` margins corrected; `SetMargins()` in `App.cs`
- ✅ Import / Export — Backup, Restore, Export Campaign Data, Import Campaign Data; `.dndx` JSON format
- ✅ All text areas auto-expand in edit mode (`WikiNotes`)
- ✅ Entity name fields select-all on focus (all detail panes)
- ✅ Lightbox cursor — pointer hand on carousel, Move cursor in lightbox
- ✅ OS-native file dialog (`DisplayServer.FileDialogShow()`)
- ✅ Navbar built out — campaign name, back button, settings menu
- ✅ "Acquainted with" NPC relationship seed added
- ✅ Notes last in all detail panes
- ✅ Detail pane footer — 24px breathing room at window bottom
- ✅ New sessions always append to end of sidebar list
- ✅ Settings menu tooltips
- ✅ Three-column layout (sidebar / detail / wiki panel)
- ✅ Quests entity + Quest History sub-feature
- ✅ NPC–NPC relationships — `character_relationships` table, full CRUD, `NpcDetailPane` panel

---

## UX Polish (2026-03-20)
- ✅ U4 — TypeOptionButton auto-select on add: `AutoSelectOnAdd = true` added before `Setup()` on all type dropdowns across all detail panes.

---

## Tab system (2026-03-20)
- ✅ F13 — Full tab system: `TabEntry`, `BuildTabWidget`, `BuildAddTabWidget`, navigation priority (existing tab → unpinned current → unpinned other → new tab), right-click context menu (Close / Close All / Close All to Right / Pin/Unpin), drag-and-drop reorder with ghost preview, tab scrollbar hidden, live theme switching via `OnTabThemeChanged`.
- ✅ F14 — "Remember Tabs" setting — full tab state serialized to JSON, persisted per-campaign via `app_settings`. Toggled via checkable Settings menu item.

---

## NPC Relationships (2026-03-22)
- ✅ F15 — NPC–NPC relationship directionality. One-directional and independent per NPC. `to_type_id` column added (unused, additive-only rule). Display: `"{currentNpc}, {type} {otherNpc}"`. `TypeOptionButton` hover-×-disappear bug fixed.

---

## Image standardization + Backup/Restore + Export/Import images (2026-03-22)
- ✅ F1 — Images copied to managed `img/{campaignName}/` folder. GUID filenames. Relative paths stored in DB. `ResolveToAbsolute()` handles legacy absolute paths. Deleting carousel image also deletes file.
- ✅ Legacy image path migration — `MigrateLegacyImagePaths(campaignId)` runs on every campaign open. Idempotent.
- ✅ Backup with images — `.zip` via `ZipFile`: `campaign.db` + `-wal`/`-shm` + `img/` folder. Restore accepts `.zip` or `.db`. `SqliteConnection.ClearAllPools()` in `Disconnect()` required to release file handle.
- ✅ F10 — Image export/import in `.dndx` packages. `EntityImageExport` model. `BuildPackage()` resolves paths before reading. `ApplyPackage()` writes decoded bytes to `img/`.

---

## Session Related-Links Panel (2026-03-22)
- ✅ F3 — `RefreshRelatedLinks()` parses `[[...]]` links from session notes, groups into collapsible sections by entity type, gold buttons for resolved links, grey disabled for unresolved. Hidden when empty. Fires live on `_notes.TextChanged`.

---

## UX Polish (2026-03-22)
- ✅ U5 — WikiNotes bullet continuation: `- ` prefix auto-continues on Enter; empty bullet body removes prefix.

---

## WikiNotes improvements (2026-03-22)
- ✅ F17 — Items and Quests added to `[[` autocomplete and link rendering.
- ✅ F18 — Stub creation via `[[+NoteType]]` syntax. Typing `[[+NPC]]` etc. opens name-prompt popup, creates stub, replaces trigger with `[[Name]]`, refreshes sidebar via `EntityCreated` signal.

---

## Skills System (2026-03-22)
- ✅ `dnd5e_skills` table — 18 standard skills seeded per campaign
- ✅ `dnd5e_character_skills` table — proficiency + expertise per character; `Source` field (`class`/`background`/`feat`/`custom`)
- ✅ `dnd5e_backgrounds` table — 16 PHB 2024 backgrounds seeded; `SkillNames` comma-separated
- ✅ `SkillExpectationService` — derives expected skill counts from class/background/feat sources; never stored
- ✅ `PlayerCharacterDetailPane` — Skills section: 18 skill rows (prof + expertise checkboxes, bonus label), source chips (✓/⚠/!), background picker, source inference on check
- ✅ Background picker modal — list + detail view; `Confirmed` event writes `BackgroundId` to PC and syncs background skills

---

## Navigation (2026-03-22)
- ✅ F2 — Per-tab back/forward navigation. `TabHistory` class. Mouse side-buttons (Xbutton1/2) wired in `_UnhandledInput`.

---

## Nested Locations + Remember Last Entity (2026-03-22)
- ✅ F6 — Nested locations in sidebar — tree structure, ▶/▼ toggle, sub-locations indented
- ✅ F14 (extended) — Remember last opened entity per campaign via full tab restoration

---

*Append-only · TTRPG Companion App · Completed Action Plan*
