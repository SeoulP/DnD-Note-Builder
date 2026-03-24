# D&D 5.5e Character System Refactor — Action Plan

## Goal
Refactor the data model to support:
- Strong relational connections (no string matching)
- Shared resource pools (Second Wind, Superiority Dice, etc.)
- Live session tracking (remaining uses, current values)
- Future expansion (spells, multiclassing, etc.)

---

## Phase 1 — Core Resource System (DO THIS FIRST)

### 1. Add Models

#### AbilityCost
- File: `Core/Models/AbilityCost.cs`
- Purpose: Link abilities to resource costs

```csharp
public class AbilityCost
{
    public int AbilityId      { get; set; }
    public int ResourceTypeId { get; set; }
    public int Amount         { get; set; } = 1;
}
```

#### CharacterResource
- File: `Core/Models/CharacterResource.cs`
- Purpose: Track current/max values for resources per character

```csharp
public class CharacterResource
{
    public int CharacterId    { get; set; }
    public int ResourceTypeId { get; set; }

    public int CurrentAmount  { get; set; } = 0;
    public int MaximumAmount  { get; set; } = 0;

    public string ValueText   { get; set; } = ""; // e.g. "d8"
    public string Notes       { get; set; } = "";
}
```

---

### 2. Update Existing Models

#### CharacterAbility (REMOVE uses tracking)

```csharp
public class CharacterAbility
{
    public int CharacterId    { get; set; }
    public int AbilityId      { get; set; }

    public string SourceType  { get; set; } = "auto";
    public int? SourceEntityId { get; set; }

    public bool IsPrepared    { get; set; }
    public bool IsPinned      { get; set; }
}
```

#### AbilityResourceType (EXPAND)

```csharp
public class AbilityResourceType
{
    public int Id              { get; set; }
    public int CampaignId      { get; set; }

    public string Name         { get; set; } = "";
    public string Description  { get; set; } = "";

    public string ResourceKind { get; set; } = ""; // Uses, Dice, Points, Slots
    public string RecoveryType { get; set; } = ""; // ShortRest, LongRest, None

    public bool TracksValueText { get; set; } = false;
}
```

---

### 3. Database Changes

#### ability_costs

```sql
CREATE TABLE ability_costs (
    ability_id INTEGER NOT NULL,
    resource_type_id INTEGER NOT NULL,
    amount INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (ability_id, resource_type_id)
);
```

#### character_resources

```sql
CREATE TABLE character_resources (
    character_id INTEGER NOT NULL,
    resource_type_id INTEGER NOT NULL,
    current_amount INTEGER NOT NULL DEFAULT 0,
    maximum_amount INTEGER NOT NULL DEFAULT 0,
    value_text TEXT NOT NULL DEFAULT '',
    notes TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (character_id, resource_type_id)
);
```

---

## Phase 2 — Fighter Implementation

### 1. Seed Resource Types

Create once, referenced everywhere by ID:

- Second Wind Uses
- Action Surge Uses
- Superiority Dice
- Indomitable Uses

---

### 2. Seed Abilities

Examples:

- Second Wind
- Tactical Mind
- Action Surge
- Precision Attack
- Trip Attack

---

### 3. Link Ability Costs

```text
Second Wind      → Second Wind Uses (1)
Tactical Mind    → Second Wind Uses (1)
Action Surge     → Action Surge Uses (1)
All Maneuvers    → Superiority Dice (1)
```

---

### 4. Character Initialization

When creating a Fighter:

- Add CharacterResources:
  - Second Wind Uses = 1
  - Action Surge Uses = 1
  - Superiority Dice = 4 (ValueText = "d8")
- Add CharacterAbilities from class levels

---

## Phase 3 — Runtime Logic

### Spend Resource Flow

When ability is used:

1. Load ability costs
2. For each cost:
   - Find CharacterResource
   - Subtract amount
3. Save updated resource state

---

### Rest Recovery

Use `RecoveryType`:

- Short Rest → restore matching resources
- Long Rest → restore all long rest resources

---

## Phase 4 — Multiclass Support (Optional but Recommended)

### Add CharacterClass

```csharp
public class CharacterClass
{
    public int CharacterId { get; set; }
    public int ClassId     { get; set; }
    public int Level       { get; set; }
    public int? SubclassId { get; set; }
}
```

### Database

```sql
CREATE TABLE character_classes (
    character_id INTEGER NOT NULL,
    class_id INTEGER NOT NULL,
    subclass_id INTEGER,
    level INTEGER NOT NULL,
    PRIMARY KEY (character_id, class_id)
);
```

---

## Phase 5 — Future Expansion (Do Later)

- Spell slots as resources
- Sorcery points
- Prepared spell tracking
- Resource scaling tables (level-based)
- UI for resource tracking

---

## Key Rules (Do Not Break These)

- NEVER reference resources by string name
- ALWAYS use IDs for relationships
- Abilities DO NOT store usage counts
- Resources store ALL runtime values
- Costs are defined via join tables, not inline fields

---

## Minimal Success Criteria

You are done when:

- Fighter can:
  - Use Second Wind
  - Use Action Surge
  - Spend Superiority Dice
- All values update live
- No strings are used for linking
- Everything is ID-based

---

## Optional Nice-To-Haves

- UI buttons for “Spend” / “Restore”
- Session log of ability usage
- Auto-recovery on rest

---

## Final Note

Do NOT overbuild formulas or scaling yet.
Hardcode Fighter values first.
Get the system working.
Then generalize.

---
