# Action Plan: Party Character Skills System

## Goal
Add a Skills section to the Player Character Detail Pane that:
- Displays all 18 D&D 5e skills with proficiency checkbox, expertise checkbox, and calculated bonus
- Tracks expected skill slots from Class, Background, and Feats
- Allows flexible editing (no hard locks) — warns on over/under selection
- Supports custom skills

> **System prefix:** All new tables use `dnd5e_` prefix (system-specific data, hotswappable per project standards)

---

## Phase 1: Data Layer

### 1.1 — `dnd5e_skills` table (new, seeded)

Seed data: the 18 standard D&D 5e skills. Seeded once per campaign via `SeedAllCampaigns`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `CampaignId` | int FK | |
| `Name` | text | e.g. “Acrobatics” |
| `Attribute` | text | `”str”` / `”dex”` / `”con”` / `”int”` / `”wis”` / `”cha”` |

Seed data (Name → Attribute):
- Acrobatics → dex, Animal Handling → wis, Arcana → int, Athletics → str
- Deception → cha, History → int, Insight → wis, Intimidation → cha
- Investigation → int, Medicine → wis, Nature → int, Perception → wis
- Performance → cha, Persuasion → cha, Religion → int, Sleight of Hand → dex
- Stealth → dex, Survival → wis

### 1.2 — `dnd5e_character_skills` table (new)

Stores which skills a character is proficient in, and at what level.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `PlayerCharacterId` | int FK | FK → `player_characters.Id` |
| `SkillId` | int FK | FK → `dnd5e_skills.Id` |
| `Source` | text | `”class”` / `”background”` / `”feat”` / `”custom”` |
| `SourceId` | int? | FK to source entity if applicable (e.g. AbilityId for feat) |
| `IsExpertise` | bool | True = double proficiency bonus |

Unique constraint: `(PlayerCharacterId, SkillId)` — a character can only have one proficiency entry per skill (expertise is a flag on the same row).

### 1.3 — `dnd5e_backgrounds` table (new, seeded)

Minimal background entity. Only needs name and skill grant count for the expectation system.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `CampaignId` | int FK | |
| `Name` | text | e.g. “Acolyte” |
| `SkillCount` | int | Number of skill proficiencies granted (almost always 2) |
| `Description` | text | Optional flavour |

Seed with the 2024 PHB backgrounds.

### 1.4 — `player_characters` migration (additive)

Add one nullable column:

```sql
ALTER TABLE player_characters ADD COLUMN background_id INTEGER;
```

`BackgroundId` (int?) on the `PlayerCharacter` model — FK → `dnd5e_backgrounds.Id`.

### 1.5 — New Repositories / Registration

- `DnD5eSkillRepository` — GetAll(campaignId), Get(id)
- `DnD5eBackgroundRepository` — GetAll(campaignId), Get(id)
- `DnD5eCharacterSkillRepository` — GetForCharacter(pcId), Upsert, Delete, DeleteAllForCharacter

All three registered in `DatabaseService.cs` (field, `InitConnection`, `RunMigrations`, `SeedAllCampaigns`).

---

## Phase 2: Expectation System (pure calculation, no DB writes)

### 2.1 — `SkillExpectation` model

```csharp
public class SkillExpectation
{
    public string Source       { get; set; }  // “class” / “background” / “feat”
    public int    SourceId     { get; set; }  // entity id (ClassId, BackgroundId, AbilityId)
    public string SourceName   { get; set; }  // display label
    public int    ExpectedCount { get; set; }
}
```

### 2.2 — `SkillExpectationService`

Responsibility: given a `PlayerCharacter`, return `List<SkillExpectation>`. Does not read or write `dnd5e_character_skills`.

**Class expectations:**
- For each `ClassLevel` ≤ `pc.Level` in the character's class, find `Ability` entries where `ChoicePoolType == “skill”`
- Sum `ResolveChoiceCount(ability, pc.Level, pc)` for those abilities
- Output one `SkillExpectation` with `Source = “class”`, `SourceId = pc.ClassId`, `ExpectedCount = total`

**Background expectations:**
- If `pc.BackgroundId` has a value, look up `dnd5e_backgrounds.SkillCount`
- Output one `SkillExpectation` with `Source = “background”`, `SourceId = pc.BackgroundId`

**Feat expectations:**
- Look at all abilities owned by the character (via `GetAllOwnedAbilities`) that are NOT from class levels — i.e. abilities not linked through `ability_class_levels`
- Among those, find any with `ChoicePoolType == “skill”`
- For each, output a `SkillExpectation` with `Source = “feat”`, `SourceId = ability.Id`, `SourceName = ability.Name`, `ExpectedCount = ResolveChoiceCount(...)`

> **Note on feat identification:** Feats are abilities that arrive via species, subspecies, or directly attached to the character — not through class level progression. The owned-but-not-class-level distinction is the separator. Revisit if subclass skills are needed.

---

## Phase 3: Bonus Calculation (pure math, no storage)

All values are derived at render time. Nothing extra is persisted.

**Proficiency bonus by level:**
```
profBonus = 2 + ((level - 1) / 4)   // integer division
```
Level 1–4 → +2, 5–8 → +3, 9–12 → +4, 13–16 → +5, 17–20 → +6

**Skill bonus:**
```
attrMod = (attrScore - 10) / 2       // floor
bonus   = attrMod
        + (IsExpertise ? 2 * profBonus : isProficient ? profBonus : 0)
```

**Attribute score lookup** (from `PlayerCharacter`):
```
“str” → Strength, “dex” → Dexterity, “con” → Constitution,
“int” → Intelligence, “wis” → Wisdom, “cha” → Charisma
```

---

## Phase 4: UI Component — `SkillRow`

A single reusable component rendered per skill. Built procedurally in code (no separate .tscn needed unless it grows complex).

**Layout (HBoxContainer):**

```
[ ☑ Prof ] [ ☑ Exp ]   Acrobatics (DEX)      +5
```

| Element | Type | Notes |
|---------|------|-------|
| Proficiency checkbox | `CheckBox` | Toggles proficiency; unchecking also clears expertise |
| Expertise checkbox | `CheckBox` | Enabled only when proficient; `Disabled = !isProficient` |
| Skill name + attribute | `Label` | e.g. “Acrobatics (DEX)”, `SizeFlagsHorizontal = ExpandFill` |
| Bonus | `Label` | e.g. “+5” or “−1”, right-aligned |

Source tag: small muted label or icon appended to the name when a source is attached (e.g. “[Class]”). Hidden for unsourced/custom.

---

## Phase 5: UI Structure — Skills Section in `PlayerCharacterDetailPane`

Placed after the ability scores block, before Ability Choices.

### Background picker

Background is selected via a dedicated **BackgroundPickerModal** (Window subclass), not an inline dropdown. The detail pane shows a read-only row:

```
Background:   Acolyte   [ Change ]
```

- `[ Change ]` button opens the modal
- If no background is set, label reads "(None)" and button reads `[ Choose ]`

**BackgroundPickerModal layout:**
- Left panel: scrollable list of all `dnd5e_backgrounds` for the campaign — clicking a row selects it and populates the right panel
- Right panel: detail view for the selected background
  - Name (large)
  - Description
  - Skill grants: lists the skills this background contributes toward (count + optional fixed list if seeded)
  - Future fields (tool proficiencies, origin feat, equipment) can be added to the model and shown here without changing the modal shell
- Footer: `[ Confirm ]` / `[ Cancel ]` buttons
- C# event `event Action<int?> BackgroundSelected` — passes the chosen `BackgroundId` (or null to clear)

On confirm: detail pane calls `Save()` and `LoadSkills()` to refresh the source chips and skill bonus labels.

**`dnd5e_backgrounds` model additions** (beyond Phase 1.3):
- `SkillNames` (text, optional) — comma-separated fixed skill names if the background prescribes specific skills rather than free picks. Empty = player chooses freely up to `SkillCount`. Used by the modal's detail view and the Auto-Fill logic.

### Skills list header

```
Skills                          [ Auto-Fill ]
───────────────────────────────────────────
Class (2/2) ✓ | Background (1/2) ⚠ | Feats (1/1) ✓ | Custom
```

Status chips per source:
- `X/Y` — current vs expected
- ✓ = met, ⚠ = under, ⚠+ = over

### Skills list body

All 18 skills rendered as `SkillRow` entries, always visible. Custom skills appended below. Sorted: standard skills alphabetically, custom at the bottom.

### Add Custom Skill button

`”+ Custom Skill”` at the bottom of the list. Opens a small inline input for name + attribute selection. Creates a `dnd5e_character_skills` entry with `Source = “custom”`.

---

## Phase 6: UI Behavior

### Proficiency toggle
- User checks proficiency → creates `dnd5e_character_skills` entry with inferred source (see below), recalculates bonus label
- User unchecks proficiency → deletes the entry (after confirm if expertise was also set), recalculates
- No hard locks — user can check any skill regardless of source counts

### Expertise toggle
- Only enabled when skill is proficient
- Toggling expertise updates `IsExpertise` on the existing row, recalculates bonus

### Source inference on check
When a user checks proficiency on a skill with no explicit source:
- If class expectation is under → assign `Source = “class”`
- Else if background expectation is under → assign `Source = “background”`
- Else if feat expectation is under → assign `Source = “feat”`
- Else → assign `Source = “custom”`

Source is informational only — it drives the status chip counts, not permissions.

### Status chip logic
After any change, re-count `dnd5e_character_skills` entries per source and compare to `SkillExpectation` values. Update chip text and icon (✓ / ⚠).

### Auto-Fill
- Collects all skills the character is NOT currently proficient in
- Fills in order of source need: class first, then background, then feat
- Stops when all expected slots are filled
- Never overwrites existing proficiency entries

---

## Phase 7: Future-Proofing

### Multiclass
- Phase 4 (Abilities Refactor plan) adds `character_classes` table
- `SkillExpectationService` extends to iterate over multiple `CharacterClass` entries
- No structural changes needed — the service is already source-agnostic

### Saving Throws
- Same pattern: `dnd5e_character_saving_throws` join table, `SaveThrowRow` component
- Reuses the proficiency bonus calculation helper

### Strict Mode (optional, not in this plan)
- Toggle on the pane: “Enforce Rules”
- Disables checking a skill in a source section when that section is already full

---

## Architecture Notes

- Expectations are **never stored** — always computed from current class/background/feat data
- `dnd5e_character_skills` is the single source of truth for what the character has
- Bonus labels are **never stored** — always derived at render time from scores + proficiency flag
- `SkillExpectationService` lives in `Core/` — no Godot UI dependencies

---

## Done Criteria

- All 18 skills visible in the detail pane at all times
- Proficiency and expertise checkboxes work and persist
- Calculated bonus label reflects scores + proficiency + expertise
- Source chips show expected vs actual count (✓ / ⚠)
- Background picker wired and saves to `player_characters.background_id`
- Auto-fill fills open slots in source order
- Custom skills can be added and removed
- No hard locks — user can select freely regardless of counts
