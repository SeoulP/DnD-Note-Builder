using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eFeatRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eFeatRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_feats (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id    INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name           TEXT    NOT NULL,
                feat_type_id   INTEGER NOT NULL REFERENCES pathfinder_feat_types(id),
                class_id       INTEGER REFERENCES pathfinder_classes(id),
                ancestry_id    INTEGER REFERENCES pathfinder_ancestries(id),
                level_required INTEGER NOT NULL DEFAULT 1,
                action_cost_id INTEGER NOT NULL REFERENCES pathfinder_action_costs(id),
                trigger        TEXT    NOT NULL DEFAULT '',
                prerequisites  TEXT    NOT NULL DEFAULT '',
                description    TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eFeat> GetAll(int campaignId)
        {
            var list = new List<Pf2eFeat>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, feat_type_id, class_id, ancestry_id, level_required, action_cost_id, trigger, prerequisites, description FROM pathfinder_feats WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eFeat Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, feat_type_id, class_id, ancestry_id, level_required, action_cost_id, trigger, prerequisites, description FROM pathfinder_feats WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eFeat f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_feats (campaign_id, name, feat_type_id, class_id, ancestry_id, level_required, action_cost_id, trigger, prerequisites, description)
                VALUES (@cid, @name, @ftype, @clid, @anc, @lvl, @act, @trig, @prereq, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",    f.CampaignId);
            cmd.Parameters.AddWithValue("@name",   f.Name);
            cmd.Parameters.AddWithValue("@ftype",  f.FeatTypeId);
            cmd.Parameters.AddWithValue("@clid",   f.ClassId.HasValue    ? (object)f.ClassId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@anc",    f.AncestryId.HasValue ? (object)f.AncestryId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@lvl",    f.LevelRequired);
            cmd.Parameters.AddWithValue("@act",    f.ActionCostId);
            cmd.Parameters.AddWithValue("@trig",   f.Trigger);
            cmd.Parameters.AddWithValue("@prereq", f.Prerequisites);
            cmd.Parameters.AddWithValue("@desc",   f.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eFeat f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_feats
                SET name = @name, feat_type_id = @ftype, class_id = @clid, ancestry_id = @anc,
                    level_required = @lvl, action_cost_id = @act, trigger = @trig, prerequisites = @prereq, description = @desc
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",   f.Name);
            cmd.Parameters.AddWithValue("@ftype",  f.FeatTypeId);
            cmd.Parameters.AddWithValue("@clid",   f.ClassId.HasValue    ? (object)f.ClassId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@anc",    f.AncestryId.HasValue ? (object)f.AncestryId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@lvl",    f.LevelRequired);
            cmd.Parameters.AddWithValue("@act",    f.ActionCostId);
            cmd.Parameters.AddWithValue("@trig",   f.Trigger);
            cmd.Parameters.AddWithValue("@prereq", f.Prerequisites);
            cmd.Parameters.AddWithValue("@desc",   f.Description);
            cmd.Parameters.AddWithValue("@id",     f.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_feats WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eFeat Map(SqliteDataReader r) => new Pf2eFeat
        {
            Id            = r.GetInt32(0),
            CampaignId    = r.GetInt32(1),
            Name          = r.GetString(2),
            FeatTypeId    = r.GetInt32(3),
            ClassId       = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
            AncestryId    = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
            LevelRequired = r.GetInt32(6),
            ActionCostId  = r.GetInt32(7),
            Trigger       = r.GetString(8),
            Prerequisites = r.GetString(9),
            Description   = r.GetString(10),
        };
    }
}
