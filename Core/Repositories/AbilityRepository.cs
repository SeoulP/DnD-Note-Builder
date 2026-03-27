using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class AbilityRepository
    {
        private readonly SqliteConnection _conn;

        public AbilityRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            // abilities — system-level ability definitions
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS abilities (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                type        TEXT    NOT NULL DEFAULT '',
                action      TEXT    NOT NULL DEFAULT '',
                trigger     TEXT    NOT NULL DEFAULT '',
                cost        TEXT    NOT NULL DEFAULT '',
                uses        INTEGER NOT NULL DEFAULT 0,
                recovery    TEXT    NOT NULL DEFAULT '',
                effect      TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // class_abilities — join: class ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS class_abilities (
                class_id   INTEGER NOT NULL REFERENCES classes(id)   ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                PRIMARY KEY (class_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // subclass_abilities — join: subclass ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subclass_abilities (
                subclass_id INTEGER NOT NULL REFERENCES subclasses(id) ON DELETE CASCADE,
                ability_id  INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                PRIMARY KEY (subclass_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // species_abilities — join: species ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS species_abilities (
                species_id INTEGER NOT NULL REFERENCES species(id)   ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                PRIMARY KEY (species_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // subspecies_abilities — join: subspecies ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subspecies_abilities (
                subspecies_id INTEGER NOT NULL REFERENCES subspecies(id) ON DELETE CASCADE,
                ability_id    INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                PRIMARY KEY (subspecies_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // class_level_abilities — join: class_level ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS class_level_abilities (
                level_id   INTEGER NOT NULL REFERENCES class_levels(id) ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id)    ON DELETE CASCADE,
                PRIMARY KEY (level_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // character_abilities — per-PC ability assignments
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_abilities (
                character_id   INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                ability_id     INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                uses_remaining INTEGER NOT NULL DEFAULT 0,
                source         TEXT    NOT NULL DEFAULT 'auto',
                PRIMARY KEY (character_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // Migration: add required_level to subclass_abilities
            var hasSubLevel = _conn.CreateCommand();
            hasSubLevel.CommandText = "SELECT COUNT(*) FROM pragma_table_info('subclass_abilities') WHERE name = 'required_level'";
            if ((long)hasSubLevel.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE subclass_abilities ADD COLUMN required_level INTEGER NOT NULL DEFAULT 1";
                alter.ExecuteNonQuery();
            }

            // Migration: add uses to subclass_abilities
            var hasSubUses = _conn.CreateCommand();
            hasSubUses.CommandText = "SELECT COUNT(*) FROM pragma_table_info('subclass_abilities') WHERE name = 'uses'";
            if ((long)hasSubUses.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE subclass_abilities ADD COLUMN uses TEXT NOT NULL DEFAULT '--'";
                alter.ExecuteNonQuery();
            }

            // Migration: add source column to existing character_abilities tables
            var hasSource = _conn.CreateCommand();
            hasSource.CommandText = "SELECT COUNT(*) FROM pragma_table_info('character_abilities') WHERE name = 'source'";
            if ((long)hasSource.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE character_abilities ADD COLUMN source TEXT NOT NULL DEFAULT 'auto'";
                alter.ExecuteNonQuery();
            }

            // Choices support — additive columns on abilities
            AddAbilityColumnIfMissing("max_choices",        "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_pool_type",   "TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("cost_resource_id",   "INTEGER REFERENCES abilities(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("resource_type_id",   "INTEGER REFERENCES ability_resource_types(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("recovery_interval",       "TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("recovery_amount",       "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("type_id",           "INTEGER REFERENCES ability_types(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("pick_count_mode",       "TEXT    NOT NULL DEFAULT 'formula'");
            AddAbilityColumnIfMissing("choice_count_base",     "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_count_attribute","TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("choice_count_add_prof", "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_count_add_level","TEXT    NOT NULL DEFAULT ''");

            // ability_costs — structured resource costs per ability (source of truth for runtime logic)
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_costs (
                ability_id       INTEGER NOT NULL REFERENCES abilities(id)              ON DELETE CASCADE,
                resource_type_id INTEGER NOT NULL REFERENCES ability_resource_types(id) ON DELETE CASCADE,
                amount           INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (ability_id, resource_type_id)
            )";
            cmd.ExecuteNonQuery();

            // Seed structured costs from the legacy abilities.resource_type_id column after ensuring it exists.
            var migrateCmd = _conn.CreateCommand();
            migrateCmd.CommandText = @"
                INSERT OR IGNORE INTO ability_costs (ability_id, resource_type_id, amount)
                SELECT id, resource_type_id, 1
                FROM abilities
                WHERE resource_type_id IS NOT NULL";
            migrateCmd.ExecuteNonQuery();

            // ability_choices — predefined options for fixed-pool abilities
            var choicesCmd = _conn.CreateCommand();
            choicesCmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_choices (
                id               INTEGER PRIMARY KEY,
                ability_id       INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                name             TEXT    NOT NULL DEFAULT '',
                description      TEXT    NOT NULL DEFAULT '',
                sort_order       INTEGER NOT NULL DEFAULT 0,
                linked_ability_id INTEGER REFERENCES abilities(id) ON DELETE SET NULL
            )";
            choicesCmd.ExecuteNonQuery();

            // Migration: add linked_ability_id to existing ability_choices tables
            var hasLink = _conn.CreateCommand();
            hasLink.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ability_choices') WHERE name = 'linked_ability_id'";
            if ((long)hasLink.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE ability_choices ADD COLUMN linked_ability_id INTEGER REFERENCES abilities(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var progressionCmd = _conn.CreateCommand();
            progressionCmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_choice_progression (
                id             INTEGER PRIMARY KEY,
                ability_id     INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                required_level INTEGER NOT NULL,
                choice_count   INTEGER NOT NULL DEFAULT 0
            )";
            progressionCmd.ExecuteNonQuery();

            var characterChoicesCmd = _conn.CreateCommand();
            characterChoicesCmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_ability_choices (
                character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                ability_id   INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                choice_id    INTEGER NOT NULL REFERENCES ability_choices(id) ON DELETE CASCADE,
                PRIMARY KEY (character_id, ability_id, choice_id)
            )";
            characterChoicesCmd.ExecuteNonQuery();
        }

        private void AddAbilityColumnIfMissing(string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('abilities') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE abilities ADD COLUMN {column} {definition}";
                alter.ExecuteNonQuery();
            }
        }

        public void SeedDefaults(int campaignId)
        {
            // TEMPORARY SEED DATA — DELETE BEFORE COMMITTING

            // ── Shared ────────────────────────────────────────────────────────────
            SeedAbility(campaignId, "Ability Score Improvement",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "Increase one ability score by 2, or two ability scores by 1 (max 20 each). Alternatively, take a Feat.");

            SeedAbility(campaignId, "Extra Attack",
                type:     "Class Feature",
                action:   "Passive",
                trigger:  "Attack action",
                effect:   "Attack twice instead of once when you take the Attack action on your turn.");

            SeedAbility(campaignId, "Weapon Mastery",
                type:           "Class Feature",
                action:         "Passive",
                effect:         "Use the mastery properties of 2 Simple or Martial weapons you're proficient with. Swap choices on Long Rest.",
                maxChoices:     2,
                choicePoolType: "weapon");

            // ── Barbarian ─────────────────────────────────────────────────────────
            SeedAbility(campaignId, "Rage",
                type:     "Class Feature",
                action:   "Bonus Action",
                resourceType: "Rage",
                recovery: "Long Rest",
                effect:   "Advantage on STR checks and saving throws. +2 bonus damage on STR melee attacks (increases with level). Resistance to Bludgeoning, Piercing, and Slashing damage. Lasts 1 minute; ends early if you haven't attacked or taken damage since your last turn.");

            SeedAbility(campaignId, "Unarmored Defense",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "While not wearing armor, AC = 10 + DEX modifier + CON modifier. A shield still applies.");

            SeedAbility(campaignId, "Danger Sense",
                type:     "Class Feature",
                action:   "Passive",
                trigger:  "DEX saving throw",
                effect:   "Advantage on DEX saving throws against effects you can see (traps, spells, etc.). No benefit while Blinded, Deafened, or Incapacitated.");

            SeedAbility(campaignId, "Reckless Attack",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Before first attack on your turn",
                effect:   "Gain advantage on STR-based attack rolls this turn. Until your next turn, attack rolls against you also have advantage.");

            SeedAbility(campaignId, "Primal Knowledge",
                type:           "Class Feature",
                action:         "Passive",
                effect:         "Gain proficiency in two skills from the Barbarian list that use STR or CON.",
                maxChoices:     2,
                choicePoolType: "skill");

            SeedAbility(campaignId, "Fast Movement",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "Speed increases by 10 feet while not wearing Heavy Armor.");

            SeedAbility(campaignId, "Feral Instinct",
                type:     "Class Feature",
                action:   "Passive",
                trigger:  "Initiative roll / Surprised",
                effect:   "Advantage on Initiative rolls. If surprised at the start of combat, you can act normally on your first turn if you enter Rage before doing anything else.");

            SeedAbility(campaignId, "Instinctive Pounce",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Enter Rage",
                effect:   "As part of the Bonus Action to enter Rage, move up to half your speed.");

            SeedAbility(campaignId, "Brutal Strike",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Reckless Attack — forgo advantage on one attack",
                effect:   "Forgo advantage on one Reckless Attack. If it hits: +1d10 damage + one Brutal Strike effect (Forceful Blow or Hamstring Blow). Improves to +2d10 / 2 effects at level 13 and +3d10 / 3 effects at level 17.");

            SeedAbility(campaignId, "Relentless Rage",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Reduced to 0 HP while raging",
                effect:   "Make a CON save (DC 10, +5 per previous use this Rage). On success, drop to 1 HP instead of 0.");

            SeedAbility(campaignId, "Improved Brutal Strike",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "Brutal Strike damage improves: +2d10 at level 13 (choose 2 effects), +3d10 at level 17 (choose 3 effects).");

            SeedAbility(campaignId, "Persistent Rage",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "Rage no longer ends early unless you choose to end it or fall Unconscious.");

            SeedAbility(campaignId, "Indomitable Might",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "If your total for a STR check is less than your STR score, use your STR score instead.");

            SeedAbility(campaignId, "Primal Champion",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "STR and CON scores each increase by 4, and their maximums increase to 24.");

            // ── Fighter ───────────────────────────────────────────────────────────
            SeedAbility(campaignId, "Fighting Style",
                type:           "Class Feature",
                action:         "Passive",
                effect:         "Choose a Fighting Style from the Fighter list.",
                maxChoices:     1,
                choicePoolType: "fixed");

            SeedAbility(campaignId, "Fighting Style: Archery",
                type:   "Class Feature",
                action: "Passive",
                effect: "+2 bonus to attack rolls with ranged weapons.");

            SeedAbility(campaignId, "Fighting Style: Defense",
                type:   "Class Feature",
                action: "Passive",
                effect: "+1 bonus to AC while wearing armor.");

            SeedAbility(campaignId, "Fighting Style: Dueling",
                type:   "Class Feature",
                action: "Passive",
                effect: "+2 to damage rolls when wielding a melee weapon in one hand and no other weapons.");

            SeedAbility(campaignId, "Fighting Style: Great Weapon Fighting",
                type:   "Class Feature",
                action: "Passive",
                trigger: "Roll damage for a two-handed or versatile weapon",
                effect: "Reroll 1s and 2s on damage dice; you must use the new roll.");

            SeedAbility(campaignId, "Fighting Style: Protection",
                type:    "Class Feature",
                action:  "Reaction",
                trigger: "A creature within 5 feet of you is attacked by someone other than you",
                effect:  "Impose disadvantage on the attack roll against the target.");

            SeedAbility(campaignId, "Fighting Style: Two-Weapon Fighting",
                type:   "Class Feature",
                action: "Passive",
                effect: "Add your ability modifier to the damage roll of your off-hand attack.");

            SeedAbility(campaignId, "Second Wind",
                type:     "Class Feature",
                action:   "Bonus Action",
                resourceType: "Second Wind",
                recovery: "Short Rest or Long Rest",
                effect:   "Regain HP equal to 1d10 + Fighter level. Also enables Tactical Mind: expend a use after failing an ability check to add 1d10 to the roll. Uses increase to 2 at level 6.");

            SeedAbility(campaignId, "Tactical Mind",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Fail an ability check",
                resourceType: "Second Wind",
                effect:   "Expend a use of Second Wind to add 1d10 to the failed ability check, potentially turning failure into success.");

            SeedAbility(campaignId, "Action Surge",
                type:     "Class Feature",
                action:   "No Action",
                resourceType: "Action Surge",
                recovery: "Short Rest or Long Rest",
                effect:   "Take one additional action on your current turn. Uses increase to 2 at level 17.");

            SeedAbility(campaignId, "Tactical Shift",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Use Action Surge",
                effect:   "When you use Action Surge, move up to half your speed without provoking opportunity attacks.");

            SeedAbility(campaignId, "Indomitable",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Fail a saving throw",
                resourceType: "Indomitable",
                recovery: "Long Rest",
                effect:   "Reroll a failed saving throw using the same die; you must use the new roll. Uses increase to 2 at level 12, 3 at level 17.");

            SeedAbility(campaignId, "Master of Armaments",
                type:     "Class Feature",
                action:   "Passive",
                trigger:  "Long Rest",
                effect:   "After each Long Rest, replace any weapons chosen for Weapon Mastery with other eligible weapons.");

            SeedAbility(campaignId, "Studied Attacks",
                type:     "Class Feature",
                action:   "No Action",
                trigger:  "Miss with an attack",
                effect:   "Gain advantage on your next attack roll against the same target before the end of your next turn.");

            SeedAbility(campaignId, "Epic Boon",
                type:     "Class Feature",
                action:   "Passive",
                effect:   "Gain an Epic Boon Feat of your choice. You can take Ability Score Improvement in place of an Epic Boon Feat.");

            SeedAbility(campaignId, "Combat Superiority",
                type:           "Subclass Feature",
                action:         "Passive",
                resourceType:   "Superiority Die",
                recovery:       "Short Rest or Long Rest",
                effect:         "You learn maneuvers fueled by Superiority Dice. You start with four d8 Superiority Dice and three maneuvers. You gain more maneuvers as you level, your dice become d10s at Fighter level 10 and d12s at Fighter level 18, and you regain all expended dice when you finish a Short or Long Rest.",
                maxChoices:     3,
                choicePoolType: "fixed");

            SeedAbility(campaignId, "Student of War",
                type:     "Subclass Feature",
                action:   "Passive",
                effect:   "Gain proficiency with one Artisan's Tools of your choice.");

            SeedAbility(campaignId, "Know Your Enemy",
                type:     "Subclass Feature",
                action:   "No Action",
                trigger:  "Observe or interact with a creature outside combat",
                effect:   "After spending time observing or interacting with a creature, learn whether it is your equal, superior, or inferior in certain combat capabilities, at the DM's discretion.");

            SeedAbility(campaignId, "Improved Combat Superiority",
                type:     "Subclass Feature",
                action:   "Passive",
                effect:   "Your Superiority Dice become d10s at Fighter level 10 and d12s at Fighter level 18.");

            SeedAbility(campaignId, "Relentless",
                type:     "Subclass Feature",
                action:   "Passive",
                effect:   "When you roll Initiative and have no Superiority Dice remaining, you regain one Superiority Die.");

            SeedAbility(campaignId, "Ambush",
                type:         "Subclass Feature",
                action:       "No Action",
                trigger:      "Dexterity (Stealth) check or Initiative roll",
                resourceType: "Superiority Die",
                effect:       "Expend one Superiority Die and add it to the roll.");

            SeedAbility(campaignId, "Commander's Strike",
                type:         "Subclass Feature",
                action:       "Bonus Action",
                trigger:      "Take the Attack action",
                resourceType: "Superiority Die",
                effect:       "Forgo one attack and direct an ally who can see or hear you to make one weapon attack as a Reaction, adding the Superiority Die to the damage on a hit.");

            SeedAbility(campaignId, "Disarming Attack",
                type:         "Subclass Feature",
                action:       "No Action",
                trigger:      "Hit a creature with a weapon attack",
                resourceType: "Superiority Die",
                effect:       "Add the Superiority Die to the damage, and the target must succeed on a Strength save or drop one held item.");

            SeedAbility(campaignId, "Menacing Attack",
                type:         "Subclass Feature",
                action:       "No Action",
                trigger:      "Hit a creature with a weapon attack",
                resourceType: "Superiority Die",
                effect:       "Add the Superiority Die to the damage, and the target must succeed on a Wisdom save or have the Frightened condition until the end of your next turn.");

            SeedAbility(campaignId, "Precision Attack",
                type:         "Subclass Feature",
                action:       "No Action",
                trigger:      "Make a weapon attack roll",
                resourceType: "Superiority Die",
                effect:       "Expend one Superiority Die and add it to the attack roll before or after the roll, but before any effects of the attack are applied.");

            SeedAbility(campaignId, "Rally",
                type:         "Subclass Feature",
                action:       "Bonus Action",
                resourceType: "Superiority Die",
                effect:       "Choose a creature who can see or hear you. That creature gains Temporary Hit Points equal to the Superiority Die roll plus your Fighter level.");

            SeedAbility(campaignId, "Riposte",
                type:         "Subclass Feature",
                action:       "Reaction",
                trigger:      "A creature misses you with a melee attack",
                resourceType: "Superiority Die",
                effect:       "Make one melee weapon attack against the creature, adding the Superiority Die to the damage on a hit.");

            SeedAbility(campaignId, "Trip Attack",
                type:         "Subclass Feature",
                action:       "No Action",
                trigger:      "Hit a creature with a weapon attack",
                resourceType: "Superiority Die",
                effect:       "Add the Superiority Die to the damage, and if the target is Large or smaller it must succeed on a Strength save or fall Prone.");

            SeedAbility(campaignId, "Lunging Attack",
                type:         "Subclass Feature",
                action:       "Bonus Action",
                trigger:      "Move at least 5 feet in a straight line toward a target before hitting with a melee attack",
                resourceType: "Superiority Die",
                effect:       "Take the Dash action as a Bonus Action. If you move at least 5 ft. in a straight line immediately before hitting with a melee attack as part of the Attack action on this turn, add the Superiority Die to the attack's damage roll.");

            SeedAbility(campaignId, "Two Extra Attacks",
                type:    "Class Feature",
                action:  "Passive",
                trigger: "Attack action",
                effect:  "Attack three times instead of once when you take the Attack action on your turn.");

            // ── Champion ──────────────────────────────────────────────────────────
            SeedAbility(campaignId, "Improved Critical",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Weapon attacks score a Critical Hit on a roll of 19 or 20.");

            SeedAbility(campaignId, "Remarkable Athlete",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Add half your Proficiency Bonus (rounded up) to Strength, Dexterity, and Constitution checks. Running long jump distance increases by a number of feet equal to your STR modifier.");

            SeedAbility(campaignId, "Additional Fighting Style",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Gain an additional Fighting Style from the Fighter list.");

            SeedAbility(campaignId, "Heroic Warrior",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "When you use Second Wind, gain Temporary Hit Points equal to your Fighter level. When you use Indomitable, gain advantage on the next attack roll you make before the end of your next turn.");

            SeedAbility(campaignId, "Superior Critical",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Weapon attacks score a Critical Hit on a roll of 18–20.");

            SeedAbility(campaignId, "Survivor",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "At the start of each of your turns in combat, regain HP equal to 5 + your CON modifier if you have fewer HP than half your Hit Point Maximum and are not at 0 HP.");

            // ── Eldritch Knight ───────────────────────────────────────────────────
            SeedAbility(campaignId, "Eldritch Knight Spellcasting",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Cast spells using Intelligence. You learn Abjuration and Evocation spells from the Wizard list. One spell per level can be from any school.");

            SeedAbility(campaignId, "War Bond",
                type:   "Subclass Feature",
                action: "Bonus Action",
                effect: "Bond with up to 2 weapons after 1 hour of practice. Bonded weapons can't be disarmed. If a bonded weapon is within 60 ft and not held, summon it to your free hand as a Bonus Action.");

            SeedAbility(campaignId, "War Magic",
                type:   "Subclass Feature",
                action: "Bonus Action",
                trigger: "Cast a cantrip on your turn",
                effect: "Make one weapon attack as a Bonus Action when you cast a cantrip on your turn.");

            SeedAbility(campaignId, "Eldritch Strike",
                type:   "Subclass Feature",
                action: "No Action",
                trigger: "Hit a creature with a weapon attack",
                effect: "The creature has disadvantage on the next saving throw it makes against a spell you cast before the end of your next turn.");

            SeedAbility(campaignId, "Arcane Charge",
                type:   "Subclass Feature",
                action: "No Action",
                trigger: "Use Action Surge",
                effect: "Teleport up to 30 feet to an unoccupied space you can see when you use Action Surge.");

            SeedAbility(campaignId, "Improved War Magic",
                type:   "Subclass Feature",
                action: "Bonus Action",
                trigger: "Cast a spell on your turn",
                effect: "Make one weapon attack as a Bonus Action when you cast any spell (not just cantrips).");

            // ── Psi Warrior ───────────────────────────────────────────────────────
            SeedAbility(campaignId, "Psionic Power",
                type:   "Subclass Feature",
                action: "Varies",
                effect: "Psionic Energy Dice (d6s = twice PB; regain 1 on Short Rest, all on Long Rest). Three uses: Protective Field (Reaction — reduce damage to you or ally within 30 ft by die roll), Psionic Strike (after hitting, expend a die for extra Force damage = die + INT mod), Telekinetic Movement (Action — move one creature or object within 30 ft up to 30 ft).");

            SeedAbility(campaignId, "Telekinetic Adept",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Psi-Powered Leap: Bonus Action, expend a Psionic Energy Die to fly up to 10× your Speed until end of turn. Telekinetic Thrust: when Psionic Strike hits, target must succeed on STR save or be knocked Prone or pushed up to 10 ft.");

            SeedAbility(campaignId, "Guarded Mind",
                type:   "Subclass Feature",
                action: "Passive",
                effect: "Resistance to Psychic damage. Expend a Psionic Energy Die to end the Charmed or Frightened condition on yourself.");

            SeedAbility(campaignId, "Bulwark of Force",
                type:         "Subclass Feature",
                action:       "Bonus Action",
                recovery:     "Long Rest",
                resourceType: "Psionic Energy Die",
                effect:       "Choose up to your INT modifier (min 1) creatures within 30 ft you can see (can include yourself). Each gains Half Cover for 1 minute or until you use this feature again.");

            SeedAbility(campaignId, "Telekinetic Master",
                type:     "Subclass Feature",
                action:   "Bonus Action",
                recovery: "Long Rest",
                effect:   "Cast Telekinesis once per Long Rest (no spell slot; or expend a Psionic Energy Die to cast again). While concentrating on Telekinesis, make one weapon attack as a Bonus Action.");

            // ── Level links ───────────────────────────────────────────────────────
            SeedClassAbilityLinks(campaignId, "Barbarian", new (int level, string[] features)[]
            {
                (1,  new[] { "Rage", "Unarmored Defense", "Weapon Mastery" }),
                (2,  new[] { "Danger Sense", "Reckless Attack" }),
                (3,  new[] { "Primal Knowledge" }),
                (4,  new[] { "Ability Score Improvement" }),
                (5,  new[] { "Extra Attack", "Fast Movement" }),
                (7,  new[] { "Feral Instinct", "Instinctive Pounce" }),
                (8,  new[] { "Ability Score Improvement" }),
                (9,  new[] { "Brutal Strike" }),
                (11, new[] { "Relentless Rage" }),
                (12, new[] { "Ability Score Improvement" }),
                (13, new[] { "Improved Brutal Strike" }),
                (15, new[] { "Persistent Rage" }),
                (16, new[] { "Ability Score Improvement" }),
                (17, new[] { "Improved Brutal Strike" }),
                (18, new[] { "Indomitable Might" }),
                (19, new[] { "Ability Score Improvement" }),
                (20, new[] { "Primal Champion" }),
            });

            SeedClassAbilityLinks(campaignId, "Fighter", new (int level, string[] features)[]
            {
                (1,  new[] { "Fighting Style", "Second Wind", "Weapon Mastery" }),
                (2,  new[] { "Action Surge", "Tactical Mind" }),
                (4,  new[] { "Ability Score Improvement" }),
                (5,  new[] { "Extra Attack", "Tactical Shift" }),
                (6,  new[] { "Ability Score Improvement" }),
                (8,  new[] { "Ability Score Improvement" }),
                (9,  new[] { "Indomitable", "Master of Armaments" }),
                (11, new[] { "Two Extra Attacks" }),
                (12, new[] { "Ability Score Improvement" }),
                (13, new[] { "Studied Attacks" }),
                (14, new[] { "Ability Score Improvement" }),
                (16, new[] { "Ability Score Improvement" }),
                (19, new[] { "Ability Score Improvement" }),
                (20, new[] { "Epic Boon" }),
            });

            // Remove stale Extra Attack link at level 11 (replaced by Two Extra Attacks)
            {
                var delStale = _conn.CreateCommand();
                delStale.CommandText = @"
                    DELETE FROM class_level_abilities
                    WHERE level_id IN (
                        SELECT cl.id FROM class_levels cl
                        JOIN classes c ON c.id = cl.class_id
                        WHERE c.campaign_id = @cid AND c.name = 'Fighter' AND cl.level = 11
                    )
                    AND ability_id IN (
                        SELECT id FROM abilities WHERE campaign_id = @cid AND name = 'Extra Attack'
                    )";
                delStale.Parameters.AddWithValue("@cid", campaignId);
                delStale.ExecuteNonQuery();
            }

            // Seed uses counts into class_data for Fighter levels
            SeedClassLevelUsesData(campaignId, "Fighter", 1,  new[] { ("Second Wind", "1") });
            SeedClassLevelUsesData(campaignId, "Fighter", 2,  new[] { ("Action Surge", "1") });
            SeedClassLevelUsesData(campaignId, "Fighter", 6,  new[] { ("Second Wind", "2") });
            SeedClassLevelUsesData(campaignId, "Fighter", 9,  new[] { ("Indomitable", "1") });
            SeedClassLevelUsesData(campaignId, "Fighter", 12, new[] { ("Indomitable", "2") });
            SeedClassLevelUsesData(campaignId, "Fighter", 17, new[] { ("Action Surge", "2"), ("Indomitable", "3") });

            SeedSubclassAbilityLinks(campaignId, "Fighter", "Champion", new (string, int)[]
            {
                ("Improved Critical",        3),
                ("Remarkable Athlete",       3),
                ("Additional Fighting Style", 7),
                ("Heroic Warrior",           10),
                ("Superior Critical",        15),
                ("Survivor",                 18),
            });

            SeedSubclassAbilityLinks(campaignId, "Fighter", "Eldritch Knight", new (string, int)[]
            {
                ("Eldritch Knight Spellcasting", 3),
                ("War Bond",                     3),
                ("War Magic",                    7),
                ("Eldritch Strike",              10),
                ("Arcane Charge",                15),
                ("Improved War Magic",           18),
            });

            SeedSubclassAbilityLinks(campaignId, "Fighter", "Psi Warrior", new (string, int)[]
            {
                ("Psionic Power",      3),
                ("Telekinetic Adept",  7),
                ("Guarded Mind",       10),
                ("Bulwark of Force",   15),
                ("Telekinetic Master", 18),
            });

            SeedSubclassAbilityLinks(campaignId, "Fighter", "Battle Master", new (string, int)[]
            {
                ("Combat Superiority",          3),
                ("Student of War",              3),
                ("Know Your Enemy",             7),
                ("Improved Combat Superiority", 10),
                ("Relentless",                  15),
                // Maneuvers — available from level 3 (picked via Combat Superiority choices)
                ("Ambush",             3),
                ("Commander's Strike", 3),
                ("Disarming Attack",   3),
                ("Menacing Attack",    3),
                ("Precision Attack",   3),
                ("Rally",              3),
                ("Riposte",            3),
                ("Trip Attack",        3),
            });

            SeedSubclassAbilityUses(campaignId, "Fighter", "Battle Master", new (string, string)[]
            {
                ("Combat Superiority",          "4"),  // 4d8 at level 3; scales to 5/6/7 but tracked in ability text
                ("Ambush",             "--"),
                ("Commander's Strike", "--"),
                ("Disarming Attack",   "--"),
                ("Menacing Attack",    "--"),
                ("Precision Attack",   "--"),
                ("Rally",              "--"),
                ("Riposte",            "--"),
                ("Trip Attack",        "--"),
            });

            SeedSubclassAbilityUses(campaignId, "Fighter", "Psi Warrior", new (string, string)[]
            {
                ("Bulwark of Force",   "1"),
                ("Telekinetic Master", "1"),
            });

            // ── Ability choices ────────────────────────────────────────────────────
            SeedAbilityChoices(campaignId, "Fighting Style", new (string, string)[]
            {
                ("Archery",               "Fighting Style: Archery"),
                ("Defense",               "Fighting Style: Defense"),
                ("Dueling",               "Fighting Style: Dueling"),
                ("Great Weapon Fighting", "Fighting Style: Great Weapon Fighting"),
                ("Protection",            "Fighting Style: Protection"),
                ("Two-Weapon Fighting",   "Fighting Style: Two-Weapon Fighting"),
            });

            SeedAbilityChoices(campaignId, "Combat Superiority", new (string, string)[]
            {
                ("Ambush",             "Ambush"),
                ("Commander's Strike", "Commander's Strike"),
                ("Disarming Attack",   "Disarming Attack"),
                ("Lunging Attack",     "Lunging Attack"),
                ("Menacing Attack",    "Menacing Attack"),
                ("Precision Attack",   "Precision Attack"),
                ("Rally",              "Rally"),
                ("Riposte",            "Riposte"),
                ("Trip Attack",        "Trip Attack"),
            });

            SeedChoiceProgression(campaignId, "Combat Superiority", new (int level, int choices)[]
            {
                (3, 3),
                (7, 5),
                (10, 7),
                (15, 9),
            });

            // ── Fighter Weapon Mastery Properties ─────────────────────────────────
            SeedAbility(campaignId, "Nick",
                type:    "Class Feature",
                action:  "Passive",
                trigger: "Extra attack from Light weapon property",
                effect:  "When you make the extra attack granted by the Light property, you can make it as part of the Attack action instead of as a Bonus Action. This extra attack can only be made once per turn.");

            SeedAbility(campaignId, "Push",
                type:    "Class Feature",
                action:  "Passive",
                trigger: "Hit a creature with a Push-property weapon",
                notes:   "Large size or smaller only",
                effect:  "If you hit a creature with a weapon that has the Push property, you can push the creature up to 10 feet straight away from you if it is Large or smaller.");

            SeedAbility(campaignId, "Topple",
                type:    "Class Feature",
                action:  "Passive",
                trigger: "Hit a creature with a Topple-property weapon",
                effect:  "If you hit a creature with a weapon that has the Topple property, you can force it to make a Constitution saving throw (DC 8 + your Proficiency Bonus + the ability modifier used for the attack). On a failed save, the creature has the Prone condition.");

            // ── Orc Species Traits ────────────────────────────────────────────────
            SeedAbility(campaignId, "Relentless Endurance",
                type:     "Species Trait",
                action:   "Passive",
                trigger:  "Reduced to 0 Hit Points but not killed outright",
                recovery: "Long Rest",
                effect:   "When you are reduced to 0 Hit Points but not killed outright, you can drop to 1 Hit Point instead. Once you use this trait, you can't do so again until you finish a Long Rest.");

            SeedAbility(campaignId, "Adrenaline Rush",
                type:     "Species Trait",
                action:   "Bonus Action",
                recovery: "Short Rest or Long Rest",
                effect:   "You can take the Dash action as a Bonus Action. When you do so, you gain a number of Temporary Hit Points equal to your Proficiency Bonus. You can use this trait a number of times equal to your Proficiency Bonus, and you regain all expended uses when you finish a Short or Long Rest.");

            SeedSpeciesAbilityLinks(campaignId, "Orc", new[] { "Relentless Endurance", "Adrenaline Rush" });

            // ── Origin Feats ──────────────────────────────────────────────────────
            SeedAbility(campaignId, "Alert",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "When you roll Initiative, you can add your Proficiency Bonus to the roll. Immediately after you roll Initiative, you can swap your Initiative with the Initiative of one willing ally in the same combat. You can't make this swap if you or the ally is Incapacitated.");

            SeedAbility(campaignId, "Crafter",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You gain Tool Proficiency with three different Artisan's Tools of your choice. Whenever you buy a nonmagical item, you receive a 20 percent discount on it. When you finish a Long Rest, you can craft one item using a tool with which you have Tool Proficiency (if you have the tools). The item lasts until you finish another Long Rest.");

            SeedAbility(campaignId, "Healer",
                type:    "Feat – Origin",
                action:  "Utilize",
                trigger: "Creature within 5 feet; Healer's Kit available",
                effect:  "Expend one use of a Healer's Kit to tend to a creature within 5 feet (Utilize Action). That creature can expend one Hit Die; you roll it, and the creature regains Hit Points equal to the roll plus your Proficiency Bonus. Whenever you roll a die to restore Hit Points with a spell or this feat, you can reroll a 1 and must use the new roll.");

            SeedAbility(campaignId, "Lucky",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You have Luck Points equal to your Proficiency Bonus, regained on a Long Rest. Spend 1 to give yourself Advantage on a d20 Test, or spend 1 to impose Disadvantage on an attack roll made against you.");

            SeedAbility(campaignId, "Magic Initiate (Cleric)",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You learn two cantrips and one 1st-level spell from the Cleric spell list. Choose Intelligence, Wisdom, or Charisma as your spellcasting ability for these spells. You can cast the 1st-level spell once without a spell slot per Long Rest, and can also cast it using any spell slots you have. When you gain a new level, you can replace one chosen spell with another from the Cleric list at the same level.");

            SeedAbility(campaignId, "Magic Initiate (Druid)",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You learn two cantrips and one 1st-level spell from the Druid spell list. Choose Intelligence, Wisdom, or Charisma as your spellcasting ability for these spells. You can cast the 1st-level spell once without a spell slot per Long Rest, and can also cast it using any spell slots you have. When you gain a new level, you can replace one chosen spell with another from the Druid list at the same level.");

            SeedAbility(campaignId, "Magic Initiate (Wizard)",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You learn two cantrips and one 1st-level spell from the Wizard spell list. Choose Intelligence, Wisdom, or Charisma as your spellcasting ability for these spells. You can cast the 1st-level spell once without a spell slot per Long Rest, and can also cast it using any spell slots you have. When you gain a new level, you can replace one chosen spell with another from the Wizard list at the same level.");

            SeedAbility(campaignId, "Musician",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "You gain Tool Proficiency with three Musical Instruments of your choice. As you finish a Short Rest or Long Rest, you can play a song on a Musical Instrument you're proficient with and give Inspiration to allies who hear the song. The number of allies you can affect equals your Proficiency Bonus.");

            SeedAbility(campaignId, "Savage Attacker",
                type:    "Feat – Origin",
                action:  "Passive",
                trigger: "Hit a target with a weapon on your turn",
                effect:  "Once per turn when you hit a target with a weapon as part of the Attack Action, you can roll the weapon's damage dice twice and use either roll against the target.");

            SeedAbility(campaignId, "Skilled",
                type:    "Feat – Origin",
                action:  "Passive",
                notes:   "Repeatable.",
                effect:  "You gain Proficiency in any combination of three Skills or Tools of your choice.");

            SeedAbility(campaignId, "Tavern Brawler",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "Your Unarmed Strikes can deal Bludgeoning Damage equal to 1d4 + your Strength modifier instead of the normal damage. Whenever you roll a damage die for an Unarmed Strike, you can reroll a 1 and must use the new roll. You have Proficiency with improvised weapons. When you hit a creature with an Unarmed Strike as part of the Attack Action on your turn, you can push it 5 feet away (once per turn).");

            SeedAbility(campaignId, "Tough",
                type:    "Feat – Origin",
                action:  "Passive",
                effect:  "Your Hit Point Maximum increases by an amount equal to twice your character level when you gain this feat. Whenever you gain a level thereafter, your Hit Point Maximum increases by an additional 2 Hit Points.");
        }

        private void SeedAbility(int campaignId, string name,
            string type = "", string action = "", string trigger = "",
            string resourceType = "", string recovery = "",
            string effect = "", string notes = "",
            int maxChoices = 0, string choicePoolType = "")
        {
            var getCmd = _conn.CreateCommand();
            getCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @name LIMIT 1";
            getCmd.Parameters.AddWithValue("@cid",  campaignId);
            getCmd.Parameters.AddWithValue("@name", name);
            var existing = getCmd.ExecuteScalar();

            int abilityId;
            if (existing != null)
            {
                abilityId = (int)(long)existing;
                var upd = _conn.CreateCommand();
                upd.CommandText = @"UPDATE abilities
                    SET type=@type, action=@action, trigger=@trigger,
                        recovery=@recovery, effect=@effect, notes=@notes,
                        max_choices=@maxchoices, choice_pool_type=@pooltype
                    WHERE id=@id";
                upd.Parameters.AddWithValue("@id",         abilityId);
                upd.Parameters.AddWithValue("@type",       type);
                upd.Parameters.AddWithValue("@action",     action);
                upd.Parameters.AddWithValue("@trigger",    trigger);
                upd.Parameters.AddWithValue("@recovery",   recovery);
                upd.Parameters.AddWithValue("@effect",     effect);
                upd.Parameters.AddWithValue("@notes",      notes);
                upd.Parameters.AddWithValue("@maxchoices", maxChoices);
                upd.Parameters.AddWithValue("@pooltype",   choicePoolType);
                upd.ExecuteNonQuery();
            }
            else
            {
                var ins = _conn.CreateCommand();
                ins.CommandText = @"INSERT INTO abilities
                    (campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, cost_resource_id)
                    VALUES (@cid, @name, @type, @action, @trigger, @recovery, @effect, @notes, 0, @maxchoices, @pooltype, NULL);
                    SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("@cid",        campaignId);
                ins.Parameters.AddWithValue("@name",       name);
                ins.Parameters.AddWithValue("@type",       type);
                ins.Parameters.AddWithValue("@action",     action);
                ins.Parameters.AddWithValue("@trigger",    trigger);
                ins.Parameters.AddWithValue("@recovery",   recovery);
                ins.Parameters.AddWithValue("@effect",     effect);
                ins.Parameters.AddWithValue("@notes",      notes);
                ins.Parameters.AddWithValue("@maxchoices", maxChoices);
                ins.Parameters.AddWithValue("@pooltype",   choicePoolType);
                abilityId = (int)(long)ins.ExecuteScalar();
            }

            // Seed ability_costs for structured resource costs
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                int? resourceTypeId = GetResourceTypeId(campaignId, resourceType);
                if (resourceTypeId.HasValue)
                {
                    var costCmd = _conn.CreateCommand();
                    costCmd.CommandText = @"INSERT OR IGNORE INTO ability_costs (ability_id, resource_type_id, amount)
                                           VALUES (@aid, @rtid, 1)";
                    costCmd.Parameters.AddWithValue("@aid",  abilityId);
                    costCmd.Parameters.AddWithValue("@rtid", resourceTypeId.Value);
                    costCmd.ExecuteNonQuery();
                }
            }
        }

        // options: (choiceName, linkedAbilityName) — linkedAbilityName can be null for unlinked choices
        private void SeedAbilityChoices(int campaignId, string abilityName, (string name, string linkedAbility)[] options)
        {
            var idCmd = _conn.CreateCommand();
            idCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @name LIMIT 1";
            idCmd.Parameters.AddWithValue("@cid",  campaignId);
            idCmd.Parameters.AddWithValue("@name", abilityName);
            var result = idCmd.ExecuteScalar();
            if (result == null) return;
            int abilityId = (int)(long)result;

            for (int i = 0; i < options.Length; i++)
            {
                var (choiceName, linkedAbilityName) = options[i];

                int? linkedId = null;
                if (linkedAbilityName != null)
                {
                    var linkCmd = _conn.CreateCommand();
                    linkCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @name LIMIT 1";
                    linkCmd.Parameters.AddWithValue("@cid",  campaignId);
                    linkCmd.Parameters.AddWithValue("@name", linkedAbilityName);
                    var linkResult = linkCmd.ExecuteScalar();
                    if (linkResult != null) linkedId = (int)(long)linkResult;
                }

                var existsCmd = _conn.CreateCommand();
                existsCmd.CommandText = "SELECT id FROM ability_choices WHERE ability_id = @aid AND name = @name LIMIT 1";
                existsCmd.Parameters.AddWithValue("@aid",  abilityId);
                existsCmd.Parameters.AddWithValue("@name", choiceName);
                var existingId = existsCmd.ExecuteScalar();

                if (existingId != null)
                {
                    // Update the link on existing choice
                    var upd = _conn.CreateCommand();
                    upd.CommandText = "UPDATE ability_choices SET linked_ability_id = @link WHERE id = @id";
                    upd.Parameters.AddWithValue("@id",   (int)(long)existingId);
                    upd.Parameters.AddWithValue("@link", linkedId.HasValue ? (object)linkedId.Value : DBNull.Value);
                    upd.ExecuteNonQuery();
                }
                else
                {
                    var ins = _conn.CreateCommand();
                    ins.CommandText = "INSERT INTO ability_choices (ability_id, name, description, sort_order, linked_ability_id) VALUES (@aid, @name, '', @order, @link)";
                    ins.Parameters.AddWithValue("@aid",   abilityId);
                    ins.Parameters.AddWithValue("@name",  choiceName);
                    ins.Parameters.AddWithValue("@order", i);
                    ins.Parameters.AddWithValue("@link",  linkedId.HasValue ? (object)linkedId.Value : DBNull.Value);
                    ins.ExecuteNonQuery();
                }
            }
        }

        private void SeedChoiceProgression(int campaignId, string abilityName, (int level, int choices)[] progression)
        {
            var idCmd = _conn.CreateCommand();
            idCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @name LIMIT 1";
            idCmd.Parameters.AddWithValue("@cid", campaignId);
            idCmd.Parameters.AddWithValue("@name", abilityName);
            var result = idCmd.ExecuteScalar();
            if (result == null) return;
            int abilityId = (int)(long)result;

            foreach (var (level, choices) in progression)
            {
                var existsCmd = _conn.CreateCommand();
                existsCmd.CommandText = "SELECT id FROM ability_choice_progression WHERE ability_id = @aid AND required_level = @level LIMIT 1";
                existsCmd.Parameters.AddWithValue("@aid", abilityId);
                existsCmd.Parameters.AddWithValue("@level", level);
                var existing = existsCmd.ExecuteScalar();

                if (existing != null)
                {
                    var update = _conn.CreateCommand();
                    update.CommandText = "UPDATE ability_choice_progression SET choice_count = @count WHERE id = @id";
                    update.Parameters.AddWithValue("@count", choices);
                    update.Parameters.AddWithValue("@id", (int)(long)existing);
                    update.ExecuteNonQuery();
                    continue;
                }

                var insert = _conn.CreateCommand();
                insert.CommandText = @"INSERT INTO ability_choice_progression (ability_id, required_level, choice_count)
                                       VALUES (@aid, @level, @count)";
                insert.Parameters.AddWithValue("@aid", abilityId);
                insert.Parameters.AddWithValue("@level", level);
                insert.Parameters.AddWithValue("@count", choices);
                insert.ExecuteNonQuery();
            }
        }

        private void SeedSpeciesAbilityLinks(int campaignId, string speciesName, string[] abilityNames)
        {
            var speciesIdCmd = _conn.CreateCommand();
            speciesIdCmd.CommandText = "SELECT id FROM species WHERE campaign_id = @cid AND name = @name LIMIT 1";
            speciesIdCmd.Parameters.AddWithValue("@cid",  campaignId);
            speciesIdCmd.Parameters.AddWithValue("@name", speciesName);
            var result = speciesIdCmd.ExecuteScalar();
            if (result == null) return;
            int speciesId = (int)(long)result;

            foreach (var abilityName in abilityNames)
            {
                int abilityId = GetOrCreateAbility(campaignId, abilityName);
                var linkCmd = _conn.CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO species_abilities (species_id, ability_id) VALUES (@sid, @aid)";
                linkCmd.Parameters.AddWithValue("@sid", speciesId);
                linkCmd.Parameters.AddWithValue("@aid", abilityId);
                linkCmd.ExecuteNonQuery();
            }
        }

        private void SeedClassAbilityLinks(int campaignId, string className, (int level, string[] features)[] levelFeatures)
        {
            var classIdCmd = _conn.CreateCommand();
            classIdCmd.CommandText = "SELECT id FROM classes WHERE campaign_id = @cid AND name = @name LIMIT 1";
            classIdCmd.Parameters.AddWithValue("@cid",  campaignId);
            classIdCmd.Parameters.AddWithValue("@name", className);
            var classResult = classIdCmd.ExecuteScalar();
            if (classResult == null) return;
            int classId = (int)(long)classResult;

            foreach (var (level, features) in levelFeatures)
            {
                var levelIdCmd = _conn.CreateCommand();
                levelIdCmd.CommandText = "SELECT id FROM class_levels WHERE class_id = @cid AND level = @level LIMIT 1";
                levelIdCmd.Parameters.AddWithValue("@cid",   classId);
                levelIdCmd.Parameters.AddWithValue("@level", level);
                var levelResult = levelIdCmd.ExecuteScalar();
                if (levelResult == null) continue;
                int levelId = (int)(long)levelResult;

                foreach (var featureName in features)
                {
                    int abilityId = GetOrCreateAbility(campaignId, featureName);
                    var linkCmd = _conn.CreateCommand();
                    linkCmd.CommandText = "INSERT OR IGNORE INTO class_level_abilities (level_id, ability_id) VALUES (@lid, @aid)";
                    linkCmd.Parameters.AddWithValue("@lid", levelId);
                    linkCmd.Parameters.AddWithValue("@aid", abilityId);
                    linkCmd.ExecuteNonQuery();
                }
            }
        }

        // Seeds uses values into class_levels.class_data for abilities already linked at that level.
        // class_data format: "abilityId:uses,abilityId:uses,..."
        private void SeedClassLevelUsesData(int campaignId, string className, int level, (string abilityName, string uses)[] usesData)
        {
            var classIdCmd = _conn.CreateCommand();
            classIdCmd.CommandText = "SELECT id FROM classes WHERE campaign_id = @cid AND name = @name LIMIT 1";
            classIdCmd.Parameters.AddWithValue("@cid",  campaignId);
            classIdCmd.Parameters.AddWithValue("@name", className);
            var classResult = classIdCmd.ExecuteScalar();
            if (classResult == null) return;
            int classId = (int)(long)classResult;

            var levelCmd = _conn.CreateCommand();
            levelCmd.CommandText = "SELECT id, class_data FROM class_levels WHERE class_id = @cid AND level = @level LIMIT 1";
            levelCmd.Parameters.AddWithValue("@cid",   classId);
            levelCmd.Parameters.AddWithValue("@level", level);
            int levelId; string currentData;
            using (var r = levelCmd.ExecuteReader())
            {
                if (!r.Read()) return;
                levelId     = r.GetInt32(0);
                currentData = r.IsDBNull(1) ? "" : r.GetString(1);
            }

            // Parse existing id:uses entries
            var map = new System.Collections.Generic.Dictionary<int, string>();
            foreach (var seg in currentData.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var p = seg.Split(':', 2);
                if (p.Length == 2 && int.TryParse(p[0].Trim(), out int uid) && uid > 0)
                    map[uid] = p[1].Trim();
            }

            foreach (var (abilityName, uses) in usesData)
            {
                int abilityId = GetOrCreateAbility(campaignId, abilityName);
                map[abilityId] = uses;
            }

            string newData = string.Join(",", System.Linq.Enumerable.Select(map, kv => $"{kv.Key}:{kv.Value}"));
            var updateCmd = _conn.CreateCommand();
            updateCmd.CommandText = "UPDATE class_levels SET class_data = @data WHERE id = @lid";
            updateCmd.Parameters.AddWithValue("@data", newData);
            updateCmd.Parameters.AddWithValue("@lid",  levelId);
            updateCmd.ExecuteNonQuery();
        }

        private void SeedSubclassAbilityUses(int campaignId, string className, string subclassName, (string abilityName, string uses)[] usesData)
        {
            var subclassIdCmd = _conn.CreateCommand();
            subclassIdCmd.CommandText = @"SELECT s.id FROM subclasses s
                JOIN classes c ON c.id = s.class_id
                WHERE s.campaign_id = @cid AND c.name = @className AND s.name = @subclassName LIMIT 1";
            subclassIdCmd.Parameters.AddWithValue("@cid",         campaignId);
            subclassIdCmd.Parameters.AddWithValue("@className",   className);
            subclassIdCmd.Parameters.AddWithValue("@subclassName", subclassName);
            var subclassResult = subclassIdCmd.ExecuteScalar();
            if (subclassResult == null) return;
            int subclassId = (int)(long)subclassResult;

            foreach (var (abilityName, uses) in usesData)
            {
                int abilityId = GetOrCreateAbility(campaignId, abilityName);
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "UPDATE subclass_abilities SET uses = @uses WHERE subclass_id = @sid AND ability_id = @aid";
                cmd.Parameters.AddWithValue("@sid",  subclassId);
                cmd.Parameters.AddWithValue("@aid",  abilityId);
                cmd.Parameters.AddWithValue("@uses", uses);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSubclassAbilityLinks(int campaignId, string className, string subclassName, (string name, int level)[] features)
        {
            var subclassIdCmd = _conn.CreateCommand();
            subclassIdCmd.CommandText = @"SELECT s.id
                                          FROM subclasses s
                                          JOIN classes c ON c.id = s.class_id
                                          WHERE s.campaign_id = @cid AND c.name = @className AND s.name = @subclassName
                                          LIMIT 1";
            subclassIdCmd.Parameters.AddWithValue("@cid", campaignId);
            subclassIdCmd.Parameters.AddWithValue("@className", className);
            subclassIdCmd.Parameters.AddWithValue("@subclassName", subclassName);
            var subclassResult = subclassIdCmd.ExecuteScalar();
            if (subclassResult == null) return;
            int subclassId = (int)(long)subclassResult;

            foreach (var (featureName, level) in features)
            {
                int abilityId = GetOrCreateAbility(campaignId, featureName);
                var linkCmd = _conn.CreateCommand();
                linkCmd.CommandText = @"INSERT INTO subclass_abilities (subclass_id, ability_id, required_level) VALUES (@sid, @aid, @level)
                    ON CONFLICT(subclass_id, ability_id) DO UPDATE SET required_level = excluded.required_level";
                linkCmd.Parameters.AddWithValue("@sid",   subclassId);
                linkCmd.Parameters.AddWithValue("@aid",   abilityId);
                linkCmd.Parameters.AddWithValue("@level", level);
                linkCmd.ExecuteNonQuery();
            }
        }

        private int GetOrCreateAbility(int campaignId, string name)
        {
            var getCmd = _conn.CreateCommand();
            getCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @name LIMIT 1";
            getCmd.Parameters.AddWithValue("@cid",  campaignId);
            getCmd.Parameters.AddWithValue("@name", name);
            var existing = getCmd.ExecuteScalar();
            if (existing != null) return (int)(long)existing;

            var insertCmd = _conn.CreateCommand();
            insertCmd.CommandText = @"INSERT INTO abilities (campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order)
                VALUES (@cid, @name, '', '', '', '', '', '', 0);
                SELECT last_insert_rowid();";
            insertCmd.Parameters.AddWithValue("@cid",  campaignId);
            insertCmd.Parameters.AddWithValue("@name", name);
            return (int)(long)insertCmd.ExecuteScalar();
        }

        // ── Abilities CRUD ────────────────────────────────────────────────────

        public List<Ability> GetAll(int campaignId)
        {
            var list = new List<Ability>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id
                                FROM abilities WHERE campaign_id = @cid ORDER BY sort_order ASC, name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Ability Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id
                                FROM abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var ability = Map(reader);
            reader.Close();
            ability.Costs = GetCosts(id);
            return ability;
        }

        public int Add(Ability ability)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO abilities (campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, cost_resource_id, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id)
                                VALUES (@cid, @name, @type, @action, @trigger, @recovery, @effect, @notes, @sort, @maxchoices, @pooltype, NULL, @recoveryInterval, @recoveryAmount, @pickMode, @ccBase, @ccAttr, @ccProf, @ccLevel, @typeId);
                                SELECT last_insert_rowid();";
            Bind(cmd, ability);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Ability ability)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE abilities
                                SET name = @name, type = @type, action = @action, trigger = @trigger,
                                    recovery = @recovery, effect = @effect,
                                    notes = @notes, sort_order = @sort,
                                    max_choices = @maxchoices, choice_pool_type = @pooltype,
                                    recovery_interval = @recoveryInterval, recovery_amount = @recoveryAmount,
                                    pick_count_mode = @pickMode,
                                    choice_count_base = @ccBase, choice_count_attribute = @ccAttr,
                                    choice_count_add_prof = @ccProf, choice_count_add_level = @ccLevel,
                                    type_id = @typeId
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", ability.Id);
            Bind(cmd, ability);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Class ability links ───────────────────────────────────────────────

        public List<int> GetAbilityIdsForClass(int classId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM class_abilities WHERE class_id = @cid";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddClassAbility(int classId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO class_abilities (class_id, ability_id) VALUES (@cid, @aid)";
            cmd.Parameters.AddWithValue("@cid", classId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveClassAbility(int classId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM class_abilities WHERE class_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", classId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Subclass ability links ────────────────────────────────────────────

        public List<int> GetAbilityIdsForSubclass(int subclassId, int maxLevel = int.MaxValue)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM subclass_abilities WHERE subclass_id = @sid AND required_level <= @level";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@level", maxLevel);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public List<(int abilityId, int requiredLevel)> GetSubclassAbilityLinks(int subclassId)
        {
            var list = new List<(int, int)>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, required_level FROM subclass_abilities WHERE subclass_id = @sid ORDER BY required_level ASC, ability_id ASC";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetInt32(0), reader.GetInt32(1)));
            return list;
        }

        public void UpdateSubclassAbilityLevel(int subclassId, int abilityId, int level)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE subclass_abilities SET required_level = @level WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@aid",   abilityId);
            cmd.Parameters.AddWithValue("@level", level);
            cmd.ExecuteNonQuery();
        }

        public List<(int abilityId, string uses)> GetAbilityIdsForSubclassLevel(int subclassId, int level)
        {
            var list = new List<(int, string)>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, uses FROM subclass_abilities WHERE subclass_id = @sid AND required_level = @level";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@level", level);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetInt32(0), reader.IsDBNull(1) ? "--" : reader.GetString(1)));
            return list;
        }

        public void UpdateSubclassAbilityUses(int subclassId, int abilityId, string uses)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE subclass_abilities SET uses = @uses WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid",  subclassId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.Parameters.AddWithValue("@uses", uses);
            cmd.ExecuteNonQuery();
        }

        public void AddSubclassAbilityAtLevel(int subclassId, int abilityId, int level)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subclass_abilities (subclass_id, ability_id, required_level) VALUES (@sid, @aid, @level)";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@aid",   abilityId);
            cmd.Parameters.AddWithValue("@level", level);
            cmd.ExecuteNonQuery();
        }

        public void AddSubclassAbility(int subclassId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subclass_abilities (subclass_id, ability_id, required_level) VALUES (@sid, @aid, 1)";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSubclassAbility(int subclassId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subclass_abilities WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Species ability links ─────────────────────────────────────────────

        public List<int> GetAbilityIdsForSpecies(int speciesId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM species_abilities WHERE species_id = @sid";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddSpeciesAbility(int speciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO species_abilities (species_id, ability_id) VALUES (@sid, @aid)";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSpeciesAbility(int speciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM species_abilities WHERE species_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Subspecies ability links ──────────────────────────────────────────

        public List<int> GetAbilityIdsForSubspecies(int subspeciesId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM subspecies_abilities WHERE subspecies_id = @ssid";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddSubspeciesAbility(int subspeciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subspecies_abilities (subspecies_id, ability_id) VALUES (@ssid, @aid)";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSubspeciesAbility(int subspeciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subspecies_abilities WHERE subspecies_id = @ssid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Class level ability links ─────────────────────────────────────────

        public List<int> GetAbilityIdsForLevel(int levelId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM class_level_abilities WHERE level_id = @lid";
            cmd.Parameters.AddWithValue("@lid", levelId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddLevelAbility(int levelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO class_level_abilities (level_id, ability_id) VALUES (@lid, @aid)";
            cmd.Parameters.AddWithValue("@lid", levelId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveLevelAbility(int levelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM class_level_abilities WHERE level_id = @lid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@lid", levelId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Character ability use tracking ────────────────────────────────────

        public List<CharacterAbility> GetCharacterAbilities(int characterId)
        {
            var list = new List<CharacterAbility>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT character_id, ability_id, source FROM character_abilities WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new CharacterAbility
                {
                    CharacterId = reader.GetInt32(0),
                    AbilityId   = reader.GetInt32(1),
                    Source      = reader.GetString(2),
                });
            return list;
        }

        public void UpsertCharacterAbility(int characterId, int abilityId, string source = "auto")
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO character_abilities (character_id, ability_id, uses_remaining, source)
                                VALUES (@cid, @aid, 0, @src)
                                ON CONFLICT(character_id, ability_id) DO NOTHING";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.Parameters.AddWithValue("@src", source);
            cmd.ExecuteNonQuery();
        }

        public void RemoveCharacterAbility(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_abilities WHERE character_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveAutoAbilitiesForCharacter(int characterId, IEnumerable<int> abilityIdsToKeep)
        {
            // Remove all 'auto' sourced abilities not in the keep set
            var keepSet = new HashSet<int>(abilityIdsToKeep);
            var existing = GetCharacterAbilities(characterId);
            foreach (var ca in existing)
            {
                if (ca.Source == "auto" && !keepSet.Contains(ca.AbilityId))
                    RemoveCharacterAbility(characterId, ca.AbilityId);
            }
        }

        public List<CharacterAbilityChoice> GetCharacterAbilityChoices(int characterId, int abilityId)
        {
            var list = new List<CharacterAbilityChoice>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT character_id, ability_id, choice_id
                                FROM character_ability_choices
                                WHERE character_id = @cid AND ability_id = @aid
                                ORDER BY choice_id ASC";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new CharacterAbilityChoice
                {
                    CharacterId = reader.GetInt32(0),
                    AbilityId = reader.GetInt32(1),
                    ChoiceId = reader.GetInt32(2),
                });
            }
            return list;
        }

        public void SetCharacterAbilityChoiceSelected(int characterId, int abilityId, int choiceId, bool selected)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = selected
                ? @"INSERT OR IGNORE INTO character_ability_choices (character_id, ability_id, choice_id)
                    VALUES (@cid, @aid, @choice)"
                : @"DELETE FROM character_ability_choices
                    WHERE character_id = @cid AND ability_id = @aid AND choice_id = @choice";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.Parameters.AddWithValue("@choice", choiceId);
            cmd.ExecuteNonQuery();
        }

        public void ClearCharacterAbilityChoices(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_ability_choices WHERE character_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public List<AbilityChoiceProgression> GetChoiceProgressionForAbility(int abilityId)
        {
            var list = new List<AbilityChoiceProgression>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, ability_id, required_level, choice_count
                                FROM ability_choice_progression
                                WHERE ability_id = @aid
                                ORDER BY required_level ASC, id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AbilityChoiceProgression
                {
                    Id = reader.GetInt32(0),
                    AbilityId = reader.GetInt32(1),
                    RequiredLevel = reader.GetInt32(2),
                    ChoiceCount = reader.GetInt32(3),
                });
            }
            return list;
        }

        public int AddChoiceProgression(AbilityChoiceProgression progression)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_choice_progression (ability_id, required_level, choice_count)
                                VALUES (@aid, @level, @count);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@aid", progression.AbilityId);
            cmd.Parameters.AddWithValue("@level", progression.RequiredLevel);
            cmd.Parameters.AddWithValue("@count", progression.ChoiceCount);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void EditChoiceProgression(AbilityChoiceProgression progression)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE ability_choice_progression
                                SET required_level = @level, choice_count = @count
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", progression.Id);
            cmd.Parameters.AddWithValue("@level", progression.RequiredLevel);
            cmd.Parameters.AddWithValue("@count", progression.ChoiceCount);
            cmd.ExecuteNonQuery();
        }

        public void DeleteChoiceProgression(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_choice_progression WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int ResolveChoiceCount(Ability ability, int characterLevel, PlayerCharacter pc = null)
        {
            int picks;
            if (ability.PickCountMode == "progression")
            {
                // Base from MaxChoices/ChoiceCountBase, overridden by progression table
                picks = ability.ChoiceCountBase > 0 ? ability.ChoiceCountBase : ability.MaxChoices;
                foreach (var step in GetChoiceProgressionForAbility(ability.Id))
                    if (step.RequiredLevel <= characterLevel)
                        picks = step.ChoiceCount;
            }
            else
            {
                // Formula mode: ChoiceCountBase is primary; fall back to MaxChoices for legacy data
                picks = ability.ChoiceCountBase > 0 ? ability.ChoiceCountBase : ability.MaxChoices;
                if (!string.IsNullOrEmpty(ability.ChoiceCountAttribute) && pc != null)
                    picks += AbilityMod(GetScore(pc, ability.ChoiceCountAttribute));
                if (ability.ChoiceCountAddProf)
                    picks += ProfBonus(characterLevel);
                picks += LevelBonus(ability.ChoiceCountAddLevel, characterLevel);
            }

            return Math.Max(0, picks);
        }

        private static int AbilityMod(int score) => (int)Math.Floor((score - 10) / 2.0);
        private static int ProfBonus(int level)  => (level - 1) / 4 + 2;
        private static int LevelBonus(string mode, int level) => mode switch
        {
            "full"      => level,
            "half_down" => level / 2,
            "half_up"   => (level + 1) / 2,
            _           => 0,
        };
        private static int GetScore(PlayerCharacter pc, string attr) => attr switch
        {
            "str" => pc.Strength,
            "dex" => pc.Dexterity,
            "con" => pc.Constitution,
            "int" => pc.Intelligence,
            "wis" => pc.Wisdom,
            "cha" => pc.Charisma,
            _     => 10,
        };

        // ── Private helpers ───────────────────────────────────────────────────

        private static void Bind(SqliteCommand cmd, Ability a)
        {
            cmd.Parameters.AddWithValue("@cid",              a.CampaignId);
            cmd.Parameters.AddWithValue("@name",             a.Name);
            cmd.Parameters.AddWithValue("@type",             a.Type);
            cmd.Parameters.AddWithValue("@action",           a.Action);
            cmd.Parameters.AddWithValue("@trigger",          a.Trigger);
            cmd.Parameters.AddWithValue("@recovery",         a.Recovery);
            cmd.Parameters.AddWithValue("@effect",           a.Effect);
            cmd.Parameters.AddWithValue("@notes",            a.Notes);
            cmd.Parameters.AddWithValue("@sort",             a.SortOrder);
            cmd.Parameters.AddWithValue("@maxchoices",       a.MaxChoices);
            cmd.Parameters.AddWithValue("@pooltype",         a.ChoicePoolType);
            cmd.Parameters.AddWithValue("@recoveryInterval", a.RecoveryInterval);
            cmd.Parameters.AddWithValue("@recoveryAmount",   a.RecoveryAmount);
            cmd.Parameters.AddWithValue("@pickMode",         a.PickCountMode);
            cmd.Parameters.AddWithValue("@ccBase",           a.ChoiceCountBase);
            cmd.Parameters.AddWithValue("@ccAttr",           a.ChoiceCountAttribute);
            cmd.Parameters.AddWithValue("@ccProf",           a.ChoiceCountAddProf ? 1 : 0);
            cmd.Parameters.AddWithValue("@ccLevel",          a.ChoiceCountAddLevel);
            cmd.Parameters.AddWithValue("@typeId",            a.TypeId.HasValue ? a.TypeId.Value : DBNull.Value);
        }

        private static Ability Map(SqliteDataReader r) => new Ability
        {
            Id               = r.GetInt32(0),
            CampaignId       = r.GetInt32(1),
            Name             = r.GetString(2),
            Type             = r.GetString(3),
            Action           = r.GetString(4),
            Trigger          = r.GetString(5),
            Recovery         = r.GetString(6),
            Effect           = r.GetString(7),
            Notes            = r.GetString(8),
            SortOrder        = r.GetInt32(9),
            MaxChoices       = r.IsDBNull(10) ? 0  : r.GetInt32(10),
            ChoicePoolType   = r.IsDBNull(11) ? "" : r.GetString(11),
            RecoveryInterval     = r.IsDBNull(12) ? ""       : r.GetString(12),
            RecoveryAmount       = r.IsDBNull(13) ? 0        : r.GetInt32(13),
            PickCountMode        = r.IsDBNull(14) ? "formula": r.GetString(14),
            ChoiceCountBase      = r.IsDBNull(15) ? 0        : r.GetInt32(15),
            ChoiceCountAttribute = r.IsDBNull(16) ? ""       : r.GetString(16),
            ChoiceCountAddProf   = !r.IsDBNull(17) && r.GetInt32(17) != 0,
            ChoiceCountAddLevel  = r.IsDBNull(18) ? ""       : r.GetString(18),
            TypeId               = r.IsDBNull(19) ? null     : r.GetInt32(19),
        };

        // ── Ability costs CRUD ────────────────────────────────────────────────

        public List<AbilityCost> GetCosts(int abilityId)
        {
            var list = new List<AbilityCost>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, resource_type_id, amount FROM ability_costs WHERE ability_id = @aid ORDER BY resource_type_id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityCost
                {
                    AbilityId      = reader.GetInt32(0),
                    ResourceTypeId = reader.GetInt32(1),
                    Amount         = reader.GetInt32(2),
                });
            return list;
        }

        public void AddCost(AbilityCost cost)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO ability_costs (ability_id, resource_type_id, amount)
                                VALUES (@aid, @rtid, @amount)";
            cmd.Parameters.AddWithValue("@aid",    cost.AbilityId);
            cmd.Parameters.AddWithValue("@rtid",   cost.ResourceTypeId);
            cmd.Parameters.AddWithValue("@amount", cost.Amount);
            cmd.ExecuteNonQuery();
        }

        public void UpdateCostAmount(int abilityId, int resourceTypeId, int amount)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_costs SET amount = @amount WHERE ability_id = @aid AND resource_type_id = @rtid";
            cmd.Parameters.AddWithValue("@aid",    abilityId);
            cmd.Parameters.AddWithValue("@rtid",   resourceTypeId);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.ExecuteNonQuery();
        }

        public void RemoveCost(int abilityId, int resourceTypeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_costs WHERE ability_id = @aid AND resource_type_id = @rtid";
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.Parameters.AddWithValue("@rtid", resourceTypeId);
            cmd.ExecuteNonQuery();
        }

        private int? GetResourceTypeId(int campaignId, string resourceType)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM ability_resource_types WHERE campaign_id = @cid AND name = @name AND inactive = 0 LIMIT 1";
            cmd.Parameters.AddWithValue("@cid",  campaignId);
            cmd.Parameters.AddWithValue("@name", resourceType);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (int)(long)result;
        }

        // ── Ability Choices CRUD ──────────────────────────────────────────────

        public List<AbilityChoice> GetChoicesForAbility(int abilityId)
        {
            var list = new List<AbilityChoice>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ability_id, name, description, sort_order, linked_ability_id FROM ability_choices WHERE ability_id = @aid ORDER BY sort_order ASC, id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityChoice
                {
                    Id              = reader.GetInt32(0),
                    AbilityId       = reader.GetInt32(1),
                    Name            = reader.GetString(2),
                    Description     = reader.GetString(3),
                    SortOrder       = reader.GetInt32(4),
                    LinkedAbilityId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                });
            return list;
        }

        public int AddChoice(AbilityChoice choice)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_choices (ability_id, name, description, sort_order, linked_ability_id)
                                VALUES (@aid, @name, @desc, @sort, @link);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@aid",  choice.AbilityId);
            cmd.Parameters.AddWithValue("@name", choice.Name);
            cmd.Parameters.AddWithValue("@desc", choice.Description);
            cmd.Parameters.AddWithValue("@sort", choice.SortOrder);
            cmd.Parameters.AddWithValue("@link", choice.LinkedAbilityId.HasValue ? (object)choice.LinkedAbilityId.Value : DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void EditChoice(AbilityChoice choice)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_choices SET name = @name, description = @desc, sort_order = @sort, linked_ability_id = @link WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",   choice.Id);
            cmd.Parameters.AddWithValue("@name", choice.Name);
            cmd.Parameters.AddWithValue("@desc", choice.Description);
            cmd.Parameters.AddWithValue("@sort", choice.SortOrder);
            cmd.Parameters.AddWithValue("@link", choice.LinkedAbilityId.HasValue ? (object)choice.LinkedAbilityId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void DeleteChoice(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_choices WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
