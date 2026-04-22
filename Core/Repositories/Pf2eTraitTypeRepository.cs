using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eTraitTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, string description)[] Defaults =
        {
            ("Mindless",        "Cannot perform actions requiring a mind. Immune to mental effects and fear."),
            ("Undead",          "Animated by void energy. Harmed by vitality, healed by void."),
            ("Construct",       "Built, not born. Immune to bleed, disease, poison, death effects, healing, mental."),
            ("Animal",          "Non-humanoid creature of relatively low intelligence."),
            ("Dragon",          "Draconic nature. Subject to Dragonslayer effects."),
            ("Humanoid",        "Generally human shape. Affected by effects that target humanoids."),
            ("Beast",           "Animalistic intelligence and instincts."),
            ("Swarm",           "Mass of tiny creatures acting as one. Immune to grabbed, prone, restrained."),
            ("Troop",           "Large formation acting as one creature with segment-based HP thresholds."),
            ("Incorporeal",     "No physical form. Immune to all physical damage."),
            ("Aquatic",         "Breathes water. Suffocates in air unless noted."),
            ("Amphibious",      "Can breathe both air and water."),
            ("Ooze",            "Amorphous. Immune to critical hits, prone, grabbed, restrained."),
            ("Plant",           "Immune to mental effects."),
            ("Fungus",          "Immune to mental effects."),
            ("Fiend",           "Native to the planes of evil."),
            ("Celestial",       "Native to the planes of good."),
            ("Elemental",       "Composed of elemental matter."),
            ("Unholy",          "Aligned with unholy power. Takes extra damage from holy effects."),
            ("Holy",            "Aligned with holy power. Takes extra damage from unholy effects."),
            ("Poison",          "This effect involves a poison."),
            ("Fear",            "This effect causes fear."),
            ("Emotion",         "Targets emotions. Mindless creatures are immune."),
            ("Mental",          "Targets the mind. Mindless creatures and constructs are immune."),
            ("Death",           "Can cause death. Undead and constructs are typically immune."),
            ("Disease",         "Involves a disease. Constructs and undead are typically immune."),
            ("Curse",           "Persistent magical effect."),
            ("Incapacitation",  "Effect is reduced by one step if the target is a higher level than the source."),
            ("Concentration",   "Requires mental focus."),
            ("Flourish",        "Can only be used once per turn."),
            ("Press",           "Can only be used if you have already made an attack this turn."),
            ("Open",            "Can only be used as your first action on your turn."),
            ("Stance",          "Entering this stance replaces any other stance you are in."),
            ("Polymorph",       "Transforms the target's body."),
            ("Teleportation",   "Moves the creature instantaneously."),
            ("Auditory",        "Effect requires hearing. Deaf creatures are unaffected."),
            ("Visual",          "Effect requires sight. Blind creatures are unaffected."),
            ("Agile",           "MAP penalty is -4/-8 instead of -5/-10."),
            ("Deadly",          "On a critical hit, add one extra damage die."),
            ("Disarm",          "Can use this weapon to Disarm with Athletics even without a free hand."),
            ("Fatal",           "On a critical hit, the weapon's damage die changes to the listed size."),
            ("Finesse",         "Can use Dexterity instead of Strength on attack rolls."),
            ("Forceful",        "Builds momentum with successive hits."),
            ("Free-Hand",       "Uses one hand but leaves the other free."),
            ("Grapple",         "Can use this weapon to Grapple with Athletics even without a free hand."),
            ("Nonlethal",       "Damage from this weapon is nonlethal unless you choose otherwise."),
            ("Parry",           "Spend one action to gain +1 circumstance bonus to AC until your next turn."),
            ("Propulsive",      "Add half Strength modifier to damage with this ranged weapon."),
            ("Ranged Trip",     "Can use this weapon to Trip at range with Athletics."),
            ("Reach",           "This weapon can attack targets up to 10 feet away."),
            ("Shove",           "Can use this weapon to Shove with Athletics even without a free hand."),
            ("Sweep",           "Gain +1 circumstance bonus to attack rolls if you attacked a different target this turn."),
            ("Thrown",          "Can throw this weapon as a ranged attack."),
            ("Topple",          "If this Strike hits and deals damage, attempt a free Trip against the target."),
            ("Trip",            "Can use this weapon to Trip with Athletics even without a free hand."),
            ("Twin",            "Next attack with this weapon type before end of your turn gains +1 circumstance bonus to damage."),
            ("Two-Hand",        "Can be wielded with two hands, changing its damage die."),
            ("Unarmed",         "This attack is an unarmed attack."),
            ("Versatile",       "This weapon can deal a different damage type."),
            ("Volley",          "Attacking within the listed range applies a -2 penalty to the attack roll."),
            ("Common",          ""),
            ("Uncommon",        ""),
            ("Rare",            ""),
            ("Unique",          ""),
            ("Arcane",          ""),
            ("Divine",          ""),
            ("Occult",          ""),
            ("Primal",          ""),
            ("Olfactory",       ""),
            ("Exploration",     ""),
            ("Downtime",        ""),
        };

        public Pf2eTraitTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_trait_types (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, description) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO pathfinder_trait_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM pathfinder_trait_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", description);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eTraitType> GetAll(int campaignId)
        {
            var list = new List<Pf2eTraitType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM pathfinder_trait_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eTraitType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM pathfinder_trait_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eTraitType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_trait_types (campaign_id, name, description) VALUES (@cid, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CampaignId);
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eTraitType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_trait_types SET name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            cmd.Parameters.AddWithValue("@id",   t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_trait_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eTraitType Map(SqliteDataReader r) => new Pf2eTraitType
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            Description = r.GetString(3),
        };
    }
}
