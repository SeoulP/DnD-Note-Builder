# PC Detail Pane — QOL Action Plan
*March 2026 · v1.0*

---

## Guiding Rules

> **No destructive migrations.** All schema changes are additive only: new columns with defaults, new tables, new join tables.
>
> **Coding conventions apply throughout.** Synchronous methods, constructor-injected `SqliteConnection`, `@param` binding, ordinal `Map()`, `Migrate()` on each repository, `Add`/`Edit`/`Delete`/`Get`/`GetAll` naming.
>
> **Layout details are provisional.** P2–P10 were planned before the new PC Detail Pane layout was finalised. All node paths, container names, and section placement references in these items should be treated as intent only and refined against the actual scene structure once P1 is complete.

---

## Status Key

| Symbol | Meaning |
|--------|---------|
| ✅ | Complete |
| ⬜ | To do |
| 🔶 | Partial / in progress |
| 🚫 | Parked / no decision yet |

---

## Open Task Summary

| # | Task | Category | Priority | Status |
|---|------|----------|----------|--------|
| P1 | PC Detail Pane layout restructure (header + nav tabs) | UI | High | ⬜ |
| P2 | Add / remove abilities on PC | Feature | High | ✅ |
| P3 | Aliases / Nicknames | Feature | High | ✅ |
| P4 | Background — feats, proficiencies, choices, write-back | Feature | High | ✅ |
| P5 | Species resource pools + level progression | Feature | High | ⬜ |
| P6 | Sections open by default + per-session state memory | UX | Medium | ⬜ |
| P7 | Missing system fields: HP, Initiative, Weapon Attacks, Items | Feature | Medium | ⬜ |
| P8 | Skill chips — granular sourcing with state icons | UX | Medium | ⬜ |
| P9 | Level Progression Usages — scaling popup (replaces flat SpinBox) | Feature | Medium | ⬜ |
| P10 | Rest Buttons | Feature | Low | ⬜ |

---

## Tasks

---

### P1 — PC Detail Pane Layout Restructure ⬜

**Context:** The current pane is a single vertical scroll. It needs to be restructured into a persistent header area and a nav-tab-driven content region, matching the agreed sketch.

**Target layout:**

```
┌─────────────────────────────────────────────────────────┐
│  [Name]           [Species / Class / Level]   [Carousel] │  ← Persistent header
│                   [Speed] [AC] [Proficiency]             │
├─────────────────────────────────────────────────────────┤
│  [⚔ Stats]  [🎒 Actions]  [📦 Inventory]  [✨ Flavor]   │  ← Icon nav buttons
├─────────────────────────────────────────────────────────┤
│                                                         │
│                  Scrollable content area                │
│               (swaps based on active tab)               │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Persistent header fields (always visible regardless of active tab):**
- Name (`LineEdit`)
- Species / Subspecies / Class / Subclass / Level (existing dropdowns + SpinBox)
- Speed, AC, Proficiency Bonus (display labels — derived or manually entered for now)
- Portrait `ImageCarousel` (top-right corner of header)
- Delete button

**Nav tab buttons:**
- Five icon-only `Button` controls in a horizontal strip below the header
- Stats, Actions, Inventory, Flavor, Spells (Spells hidden until spellcaster class is selected — deferred to Spells phase)
- Active tab tracked in `_activeTab` string field (per-session, not persisted to DB)
- Switching tabs shows/hides the corresponding content `Control`

**Tab content assignment (initial):**

| Tab | Content |
|-----|---------|
| Stats | Ability scores, saves, Skills section |
| Actions | Ability choices section, Resources section |
| Inventory | *Empty placeholder for now — P10* |
| Flavor | Description (`TextEdit`), Background row, WikiNotes |

**Scene restructure:**
- `player_character_detail_pane.tscn` — split `ContentHBox` into `HeaderSection` (fixed) and `NavTabContent` (swappable children, one per tab)
- `PlayerCharacterDetailPane.cs` — add `_activeTab`, `SetActiveTab(string name)`, show/hide per-tab containers

**Per-session tab memory:**
- `_activeTab` is an instance field — survives navigation within the session, resets to "Stats" on app reopen
- No DB persistence required

---

### P2 — Add / Remove Abilities on PC ✅

**Context:** PCs may acquire homebrew or ad-hoc abilities during play. Currently all abilities are sourced automatically from class/species. A manual add/remove path is needed.

**Model:**
`CharacterAbility` already exists with a `Source` field (`"auto"` / `"manual"`). No schema changes needed.

**Repository — `PlayerCharacterRepository` additive methods:**
```csharp
void AddManualAbility(int characterId, int abilityId);   // INSERT INTO character_abilities (character_id, ability_id, source) VALUES (@cid, @aid, 'manual')
void RemoveManualAbility(int characterId, int abilityId); // DELETE WHERE character_id = @cid AND ability_id = @aid AND source = 'manual'
List<int> GetManualAbilityIds(int characterId);
```

**UI — Actions tab (`LoadAbilityChoices`):**
- Add a `+` Button to the far right of the Abilities section header
- `+` opens a `TypeOptionButton` picker scoped to `_db.Abilities.GetAll(_pc.CampaignId)`, filtered to exclude abilities already owned (auto or manual)
- Selecting an ability calls `AddManualAbility`, then `LoadAbilityChoices`
- All ability rows in the Actions tab gain a delete `×` button when `Source == "manual"`
- `×` shows a `ConfirmationDialog`: *"This will remove the ability from this character but will not delete the ability itself. It will need to be manually deleted from the Abilities list if no longer needed."*
- On confirm: calls `RemoveManualAbility`, then `LoadAbilityChoices`

**`GetAllOwnedAbilities` update:**
- Extend to also load manual ability IDs from `GetManualAbilityIds(_pc.Id)` and merge with auto-sourced IDs

---

### P3 — Aliases / Nicknames ✅

**Context:** Characters (and other entities) often have shortened names used in notes — e.g. Zemmouregal → Zemmy. Aliases should be searchable and resolve in WikiLinks.

**Schema — new table `entity_aliases` (system-agnostic, no prefix):**

```sql
CREATE TABLE IF NOT EXISTS entity_aliases (
    id          INTEGER PRIMARY KEY,
    campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    entity_type TEXT    NOT NULL,  -- "playercharacter", "npc", "location", "faction", "item", "quest"
    entity_id   INTEGER NOT NULL,
    alias       TEXT    NOT NULL,
    UNIQUE (campaign_id, alias)    -- aliases must be unique within a campaign
);
```

**Model — `EntityAlias.cs` (new):**
```csharp
public class EntityAlias
{
    public int    Id         { get; set; }
    public int    CampaignId { get; set; }
    public string EntityType { get; set; } = "";
    public int    EntityId   { get; set; }
    public string Alias      { get; set; } = "";
}
```

**Repository — `EntityAliasRepository.cs` (new):**
- `Migrate()` — creates `entity_aliases` table
- `GetAll(int campaignId)` — returns all aliases for a campaign (used by WikiLink resolver)
- `GetForEntity(string entityType, int entityId)` — returns aliases for one entity
- `Add(EntityAlias alias)` — inserts; returns new id
- `Delete(int id)` — deletes by id

Register in `DatabaseService` after `SessionRepository` (no FK dependencies on new table).

**UI — Alias chips below Name field (all applicable detail panes):**
- A horizontal `FlowContainer` of alias chips displayed below the `_nameInput`
- Each chip: `[Zemmy ×]` — label + remove button
- A `TypeOptionButton`-style inline add control at the end of the chip row:
  - Text input for the new alias
  - On Enter/confirm: calls `EntityAliasRepository.Add`, refreshes chips
  - Duplicate within campaign shows a Toast error: *"That alias is already in use."*
- `×` on a chip calls `EntityAliasRepository.Delete`, refreshes chips
- Implement on `PlayerCharacterDetailPane` first; extend to NPC, Location, Faction, Item, Quest panes in a follow-on pass

**WikiLink resolution — `WikiLinkParser.cs` update:**
- `BuildLookup()` currently maps `name → (type, id)` for each entity type
- Extend to also load all `EntityAlias` rows for the campaign and add each `alias → (type, id)` mapping
- Alias entries resolve identically to name entries — the link navigates to the entity, but the **display text is kept as typed** (i.e. `[[Zemmy]]` renders as "Zemmy" but navigates to Zemmouregal)
- If an alias conflicts with a real entity name, entity name takes precedence

**WikiNotes autocomplete — `WikiNotes.cs` update:**
- `GetEntityMatches()` extend to also search alias text
- Show alias matches in autocomplete with a subtle label: `Zemmy (→ Zemmouregal)`

---

### P4 — Background: Feats, Proficiencies, Choices, Write-Back ✅

**Context:** The current `DnD5eBackground` model only stores `SkillNames` as a comma-separated string. The 2024 PHB Background system grants: a feat (Origin feat), skill proficiencies, tool/instrument/language proficiency, and starting equipment. Write-back means selecting a background populates the character sheet with those grants.

**Schema — additive migrations to `dnd5e_backgrounds`:**

```sql
ALTER TABLE dnd5e_backgrounds ADD COLUMN feat_ability_id INTEGER REFERENCES abilities(id) ON DELETE SET NULL DEFAULT NULL;
ALTER TABLE dnd5e_backgrounds ADD COLUMN tool_options    TEXT NOT NULL DEFAULT '';  -- comma-separated tool/instrument choices
ALTER TABLE dnd5e_backgrounds ADD COLUMN language_count  INTEGER NOT NULL DEFAULT 1;
ALTER TABLE dnd5e_backgrounds ADD COLUMN is_custom       INTEGER NOT NULL DEFAULT 0; -- 1 = custom background, 0 = standard
```

**Model updates — `DnD5eBackground.cs`:**
```csharp
public int?   FeatAbilityId  { get; set; }   // FK → abilities.id (the Origin Feat granted)
public string ToolOptions    { get; set; } = ""; // comma-separated tool choices (freetext for now)
public int    LanguageCount  { get; set; } = 1;
public bool   IsCustom       { get; set; } = false;
```

**Background Picker Modal — expanded:**
- Right panel gains:
  - **Feat** row: `TypeOptionButton` scoped to abilities of type "Feat – Origin" in the campaign
  - **Skills** row: checkboxes for all campaign skills (locked to 2 for standard, free for custom)
  - **Tools / Instruments** row: freetext or multiselect (freetext to start)
  - **Languages** row: SpinBox for count (standard = 1)
  - **Description** TextEdit (existing)
- Custom backgrounds show all fields as editable
- Standard backgrounds show feat/skill/tool/language as read-only (display only) unless the user explicitly unlocks them

**Write-back on Background Confirm — `OnBackgroundSelected` / `SyncBackgroundSkills`:**
- Existing `SyncBackgroundSkills` already handles skill grants — extend it
- Add `SyncBackgroundFeat(int? backgroundId)`:
  - Remove any `character_abilities` row where `source = "background"`
  - If new background has `FeatAbilityId`, insert `(characterId, featAbilityId, "background")` into `character_abilities`
- Call both sync methods from `OnBackgroundSelected` before `Save()`
- `LoadAbilityChoices` and `LoadSkills` already called after — no additional refresh needed

**Existing seeded backgrounds — data update:**
All 16 seeded PHB backgrounds need `feat_ability_id` set. Since these are per-campaign seeded records, this is handled at seed time: `SeedDefaults` is already idempotent via `WHERE NOT EXISTS`. The feat IDs are campaign-specific so they cannot be hard-seeded — the Background Picker Modal is the correct place for a DM to assign them. No automated seed for feats.

---

### P5 — Species Resource Pools + Level Progression ⬜

**Context:** Species currently has no resource pool or level progression system. Classes have both. Species should have the same capability (e.g. Orc's Relentless Endurance, Aasimar's Healing Hands).

**This is a direct parallel of the Class system.** Reuse the same patterns.

**Schema — new tables (species-scoped):**

```sql
CREATE TABLE IF NOT EXISTS species_levels (
    id          INTEGER PRIMARY KEY,
    species_id  INTEGER NOT NULL REFERENCES species(id) ON DELETE CASCADE,
    level       INTEGER NOT NULL,
    features    TEXT    NOT NULL DEFAULT '',
    class_data  TEXT    NOT NULL DEFAULT '',  -- reuse same "abilityId:uses,..." format as class levels
    UNIQUE (species_id, level)
);

CREATE TABLE IF NOT EXISTS ability_species_levels (
    ability_id       INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
    species_level_id INTEGER NOT NULL REFERENCES species_levels(id) ON DELETE CASCADE,
    PRIMARY KEY (ability_id, species_level_id)
);
```

**Models — new:**
- `SpeciesLevel.cs` — mirrors `ClassLevel.cs` (Id, SpeciesId, Level, Features, ClassData)

**Repository — `SpeciesRepository.cs` additive methods:**
```csharp
List<SpeciesLevel> GetLevelsForSpecies(int speciesId);
void SaveLevel(SpeciesLevel level);
void AddLevelAbility(int speciesLevelId, int abilityId);
void RemoveLevelAbility(int speciesLevelId, int abilityId);
List<int> GetAbilityIdsForSpeciesLevel(int speciesLevelId);
```

Plus `Migrate()` guards for both new tables.

**`AbilityRepository` — new query:**
```csharp
List<int> GetAbilityIdsForSpecies(int speciesId);  // already exists — verify it reads from species_levels
```
If currently reading from a direct `species_abilities` table, update to read through `species_levels` + `ability_species_levels` instead. Otherwise add the join.

**UI — `SpeciesDetailPane` additions:**
- Add a **Level Progression** section matching the Class pane's collapsible level rows
- Each level row: same `TypeOptionButton` ability picker + Usages field (see P9 for the scaling usages popup)
- "Initialize 20 levels" button (same as Class pane)

**`GetAllOwnedAbilities` in `PlayerCharacterDetailPane` update:**
- Already reads `GetAbilityIdsForSpecies(_pc.SpeciesId.Value)` — verify this traverses species levels correctly after new tables are added

**`PlayerCharacterRepository.SyncResources` update:**
- Currently syncs resources from class levels; extend to also sync from species levels

---

### P6 — Sections Open by Default + Per-Session State Memory ⬜

**Context:** Ability action sections (Action, Bonus Action, etc.) currently default to closed. This is annoying during active play. They should default to open, and open/closed state should be remembered for the session.

**Changes — `PlayerCharacterDetailPane.cs`:**

1. **Default open:** When building section toggles in `LoadAbilityChoices`, change the initial `isOpen` check:
   ```csharp
   // Before: bool isOpen = _openAbilitySections.Contains(sectionName);
   bool isOpen = !_closedAbilitySections.Contains(sectionName);  // opt-out instead of opt-in
   ```
   Rename `_openAbilitySections` → `_closedAbilitySections` (a `HashSet<string>` of sections the user has explicitly closed).

2. **Section toggle handler update:**
   ```csharp
   sectionToggle.Toggled += pressed =>
   {
       if (!pressed) _closedAbilitySections.Add(sectionName);
       else _closedAbilitySections.Remove(sectionName);
       // ... rest unchanged
   };
   ```

3. **Tab state memory (`_activeTab`):** Already defined in P1 — `_activeTab` is an instance field set on nav button press. No additional work needed here.

4. **Reset on character load:** `_closedAbilitySections.Clear()` in `Load()` so switching to a different character resets section state.

---

### P10 — Rest Buttons ⬜

**Context:** Short Rest and Long Rest buttons for resetting ability uses and resources.

**UI — header area (persistent, always visible):**
- Two `Button` controls in the persistent header: `[Short Rest]` and `[Long Rest]`
- Flat style, small, grouped together

**Logic — `PlayerCharacterDetailPane.cs`:**

```csharp
private void OnShortRest()
{
    if (_pc == null) return;
    var resources = _db.PlayerCharacters.GetResources(_pc.Id);
    foreach (var res in resources)
    {
        var type = _db.AbilityResourceTypes.Get(res.ResourceTypeId);
        if (type == null) continue;
        // RecoveryInterval: "ShortRest" | "ShortOrLongRest" → restore RecoveryAmount (0 = full)
        if (type.RecoveryInterval == "ShortRest" || type.RecoveryInterval == "ShortOrLongRest")
        {
            int restored = type.RecoveryAmount == 0 ? res.MaximumAmount
                         : Math.Min(res.MaximumAmount, res.CurrentAmount + type.RecoveryAmount);
            res.CurrentAmount = restored;
            _db.PlayerCharacters.UpsertResource(res);
        }
    }
    LoadResources();
    LoadAbilityChoices();
}

private void OnLongRest()
{
    if (_pc == null) return;
    var resources = _db.PlayerCharacters.GetResources(_pc.Id);
    foreach (var res in resources)
    {
        res.CurrentAmount = res.MaximumAmount;  // Long rest always restores all
        _db.PlayerCharacters.UpsertResource(res);
    }
    LoadResources();
    LoadAbilityChoices();
}
```

Wire `_shortRestButton.Pressed += OnShortRest` and `_longRestButton.Pressed += OnLongRest` in `_Ready`.

**Note:** `AbilityResourceType` already has `RecoveryInterval` and `RecoveryAmount` fields. No schema changes needed.

---

### P8 — Skill Chips: Granular Sourcing with State Icons ⬜

**Context:** The current skill chip row shows a single chip per source with a count and a ⚠/✓/! icon. The request is for chips to show state icons that, on hover, reveal exactly where the skill points come from.

**Chip states (per source):**

| Icon | Meaning |
|------|---------|
| ✓ | Exactly met — actual == expected |
| ⚠ | Under budget — actual < expected |
| ! | Over budget — actual > expected (warn, do not block) |

**Hover tooltip on state icon:**
- ✓ → *"{SourceName}: {actual}/{expected} skills selected"*
- ⚠ → *"Select {diff} more from {SourceName}"*
- ! → *"{-diff} over limit for {SourceName} — review your selections"*

**Changes — `BuildSourceChips` in `PlayerCharacterDetailPane.cs`:**
- Current implementation already produces per-source chips with ⚠/✓/! icons
- Update tooltip text to match the above format
- Add `TooltipText` on the state icon label (currently only on the chip as a whole)
- Separate the icon and the count label so the icon alone triggers the tooltip — hover on icon shows source detail, not just the raw count
- No structural data model changes needed

**Skill row state icon (new):**
- Each `SkillRow` gains a small state indicator to the left of the proficiency checkbox
- Icon reflects: not proficient (blank), proficient from a valid source (✓), proficient but over source budget (!)
- On hover: tooltip shows *"Source: {sourceName}"*
- This requires passing source info down to `BuildSkillRow` — already available via `cs.Source` on the `DnD5eCharacterSkill` record

---

### P9 — Level Progression Usages: Scaling Popup ⬜

**Context:** The current usages field in Class (and soon Species) level progression is a flat `SpinBox` with a single integer. This cannot express "2 uses at level 3, 3 uses at level 7, 4 uses at level 15" — the common D&D pattern. The fix is a scaling table popup, replacing the flat SpinBox. This should also replace the existing `# of Picks` formula popup if there is enough overlap.

**Data model — reuse `AbilityChoiceProgression` pattern:**
Currently `AbilityChoiceProgression` stores `(AbilityId, RequiredLevel, ChoiceCount)`. A parallel `AbilityUsageProgression` table stores level-scaled usages.

New table:

```sql
CREATE TABLE IF NOT EXISTS ability_usage_progression (
    id            INTEGER PRIMARY KEY,
    ability_id    INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
    required_level INTEGER NOT NULL,
    usages        INTEGER NOT NULL,
    UNIQUE (ability_id, required_level)
);
```

**Model — `AbilityUsageProgression.cs` (new):**
```csharp
public class AbilityUsageProgression
{
    public int Id            { get; set; }
    public int AbilityId     { get; set; }
    public int RequiredLevel { get; set; }
    public int Usages        { get; set; }
}
```

**Storage in Class Level Progression:**
The current `ClassData` column on `class_levels` stores `"abilityId:uses,..."` as a flat string. When usages are scaling, the stored value becomes `"abilityId:prog"` (sentinel meaning "use the progression table"). The actual values live in `ability_usage_progression`.

**Popup — `UsageProgressionPopup.cs` (new, reusable):**
- A small `Window` popup with a table: Level | Usages rows
- Pre-populated with existing progression rows for the ability
- Add row: level SpinBox + usages SpinBox + `+` button
- Delete row: `×` per row
- `[Save]` / `[Cancel]` footer
- `event Action<List<AbilityUsageProgression>> Saved`

**Trigger in Class/Species Level Progression row:**
Replace the flat `SpinBox` with a `Button` showing the current value (or "Scaling" if multi-row progression exists). Pressing the button opens `UsageProgressionPopup`.

**Resolve at runtime — `PlayerCharacterRepository` / `AbilityRepository`:**
```csharp
int ResolveUsagesAtLevel(int abilityId, int characterLevel);
// Returns the highest AbilityUsageProgression.Usages where RequiredLevel <= characterLevel.
// Falls back to flat value stored in ClassData if no progression rows exist.
```

**`# of Picks` overlap:**
`AbilityChoiceProgression` serves the same structural role for pick counts. Evaluate whether the `UsageProgressionPopup` can be parameterized to serve both (`ChoiceCount` vs `Usages`). If the field names and semantics align closely enough, extract a shared `LevelScalingPopup` with a configurable column label. Otherwise keep them separate.

---

### P7 — Missing System Fields: HP, Initiative, Weapon Attacks, Items ⬜

**Context:** The Inventory tab will be empty until this is built. HP, Initiative, and Weapon Attacks are combat essentials missing from the Stats tab. Items belong in the Inventory tab.

**Scope note:** This is flagged Low priority and is mostly placeholder work. The tab structure from P1 needs to exist first.

**Schema — additive migrations to `player_characters`:**

```sql
ALTER TABLE player_characters ADD COLUMN max_hp          INTEGER NOT NULL DEFAULT 0;
ALTER TABLE player_characters ADD COLUMN current_hp      INTEGER NOT NULL DEFAULT 0;
ALTER TABLE player_characters ADD COLUMN temp_hp         INTEGER NOT NULL DEFAULT 0;
ALTER TABLE player_characters ADD COLUMN armor_class     INTEGER NOT NULL DEFAULT 10;
ALTER TABLE player_characters ADD COLUMN initiative_bonus INTEGER NOT NULL DEFAULT 0;
ALTER TABLE player_characters ADD COLUMN speed           INTEGER NOT NULL DEFAULT 30;
```

**Model — `PlayerCharacter.cs` additive fields:**
```csharp
public int MaxHp          { get; set; } = 0;
public int CurrentHp      { get; set; } = 0;
public int TempHp         { get; set; } = 0;
public int ArmorClass     { get; set; } = 10;
public int InitiativeBonus { get; set; } = 0;
public int Speed          { get; set; } = 30;
```

**Stats tab — persistent header:**
- AC, Speed, Initiative Bonus displayed in the persistent header (always visible)
- HP (Current / Max / Temp) displayed in the Stats tab content area

**Inventory tab:**
- Wire `CharacterItem` (model and `CharacterItemRepository` already exist) to the Inventory tab
- Display as a simple list of items with quantity and notes
- Add/remove via `TypeOptionButton` picker scoped to campaign items

**Weapon Attacks:**
- Deferred to a dedicated Weapon Attacks section within the Actions tab
- Design TBD — likely a manually maintained list with name, attack bonus, damage, and damage type

---

## Implementation Order

Priority as discussed — items are ordered for implementation, not just importance:

1. **P1 — Layout restructure** — must come first; everything else slots into tabs
2. **P6 — Sections open by default** — trivial change, high daily QOL gain, do alongside P1
3. **P2 — Add / remove abilities** — small, self-contained, high play value
4. **P3 — Aliases / Nicknames** — new table + UI; cross-entity value
5. **P4 — Background write-back** — schema additions + modal expansion
6. **P5 — Species resource pools** — direct parallel to class system
7. **P7 — HP / Initiative / Items / Weapon Attacks** — fills the Inventory tab and Stats tab gaps
8. **P8 — Skill chip improvements** — polish pass on existing chip UI
9. **P9 — Scaling usages popup** — replaces SpinBox in Class + Species panes
10. **P10 — Rest buttons** — depends on HP being present to be fully useful

---

## File Change Summary

| File | Change | Task |
|------|--------|------|
| `player_character_detail_pane.tscn` | Restructure: HeaderSection + NavTabButtons + 4×TabContent containers | P1 |
| `PlayerCharacterDetailPane.cs` | `_activeTab`, `SetActiveTab()`, show/hide tab containers; move nodes to correct tabs | P1 |
| `PlayerCharacterDetailPane.cs` | Rename `_openAbilitySections` → `_closedAbilitySections`; default-open logic | P6 |
| `PlayerCharacterDetailPane.cs` | `AddManualAbility`, `RemoveManualAbility` wiring; orphan warning dialog; `+` button on Abilities header | P2 |
| `Core/Repositories/PlayerCharacterRepository.cs` | `AddManualAbility`, `RemoveManualAbility`, `GetManualAbilityIds` | P2 |
| `PlayerCharacterDetailPane.cs` | `GetAllOwnedAbilities` — merge manual ability IDs | P2 |
| `Core/Models/EntityAlias.cs` | **New** | P3 |
| `Core/Repositories/EntityAliasRepository.cs` | **New** — `Migrate`, `GetAll`, `GetForEntity`, `Add`, `Delete` | P3 |
| `Core/DatabaseService.cs` | Register `EntityAliasRepository`; call `Migrate()` | P3 |
| `PlayerCharacterDetailPane.cs` | Alias chip row below name input | P3 |
| `Core/WikiLinkParser.cs` | Extend `BuildLookup()` to include aliases | P3 |
| `Scenes/Components/WikiNotes/WikiNotes.cs` | Extend `GetEntityMatches()` to search aliases | P3 |
| `Core/Models/DnD5eBackground.cs` | Add `FeatAbilityId`, `ToolOptions`, `LanguageCount`, `IsCustom` | P4 |
| `Core/Repositories/DnD5eBackgroundRepository.cs` | Additive migrations for new columns | P4 |
| `Scenes/Modals/BackgroundPickerModal/BackgroundPickerModal.cs` | Feat picker, skill checkboxes, tool/language fields; custom vs standard mode | P4 |
| `PlayerCharacterDetailPane.cs` | `SyncBackgroundFeat()`; call from `OnBackgroundSelected` | P4 |
| `Core/Models/SpeciesLevel.cs` | **New** | P5 |
| `Core/Repositories/SpeciesRepository.cs` | Additive: `species_levels`, `ability_species_levels` tables + level CRUD + ability link methods | P5 |
| `Scenes/Components/SpeciesDetailPane/SpeciesDetailPane.cs` | Level Progression section | P5 |
| `PlayerCharacterDetailPane.cs` | Verify `GetAllOwnedAbilities` traverses species levels; extend `SyncResources` | P5 |
| `player_character_detail_pane.tscn` | Short Rest + Long Rest buttons in header | P10 |
| `PlayerCharacterDetailPane.cs` | `OnShortRest()`, `OnLongRest()` | P10 |
| `PlayerCharacterDetailPane.cs` | `BuildSourceChips` — improved tooltip text + icon hover | P8 |
| `PlayerCharacterDetailPane.cs` | `BuildSkillRow` — per-row source state icon + tooltip | P8 |
| `Core/Models/AbilityUsageProgression.cs` | **New** | P9 |
| `Core/Repositories/AbilityRepository.cs` | `ability_usage_progression` table migration; `GetUsageProgressionForAbility`, `SaveUsageProgression`, `ResolveUsagesAtLevel` | P9 |
| `Scenes/Components/ClassDetailPane/ClassDetailPane.cs` | Replace SpinBox with scaling popup button; sentinel `"prog"` in ClassData | P9 |
| `Scenes/Components/SpeciesDetailPane/SpeciesDetailPane.cs` | Same scaling popup button for species levels | P9 |
| `Core/Models/PlayerCharacter.cs` | Add `MaxHp`, `CurrentHp`, `TempHp`, `ArmorClass`, `InitiativeBonus`, `Speed` | P7 |
| `Core/Repositories/PlayerCharacterRepository.cs` | Additive migrations for HP/AC/Initiative/Speed columns | P7 |
| `PlayerCharacterDetailPane.cs` | HP fields in Stats tab; AC/Speed/Initiative in persistent header | P7 |
| `PlayerCharacterDetailPane.cs` | Inventory tab — `CharacterItem` list wired | P7 |

---

*Generated March 2026 · TTRPG Companion App · PC Detail Pane QOL Action Plan · v1.0*
