using GdUnit4;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.Repositories
{
    [TestSuite]
    public class QuestRewardRepositoryTest
    {
        private SqliteConnection      _conn;
        private QuestRewardRepository _rewards;
        private int _questId;

        [BeforeTest]
        public void Setup()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            using (var p = _conn.CreateCommand()) { p.CommandText = "PRAGMA foreign_keys = OFF"; p.ExecuteNonQuery(); }

            var campaigns = new CampaignRepository(_conn);
            var quests    = new QuestRepository(_conn);
            _rewards      = new QuestRewardRepository(_conn);
            campaigns.Migrate();
            quests.Migrate();
            _rewards.Migrate();

            int cid = campaigns.Add(new Campaign { Name = "C", System = "dnd5e_2024" });
            _questId = InsertQuest(_conn, cid);
        }

        [AfterTest]
        public void Teardown() => _conn?.Close();

        // QuestRepository.Add uses a multi-statement INSERT that fails with Microsoft.Data.Sqlite v10
        // in tests — use raw single-statement SQL instead.
        private static int InsertQuest(SqliteConnection conn, int campaignId, string reward = "")
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO quests (campaign_id, name, description, notes, reward) VALUES (@cid, 'Q', '', '', @reward)";
            cmd.Parameters.AddWithValue("@cid",    campaignId);
            cmd.Parameters.AddWithValue("@reward", reward);
            cmd.ExecuteNonQuery();
            var id = conn.CreateCommand();
            id.CommandText = "SELECT last_insert_rowid()";
            return (int)(long)id.ExecuteScalar();
        }

        [TestCase]
        public void GetAll_Empty_ReturnsEmptyList()
        {
            AssertThat(_rewards.GetAll(_questId).Count).IsEqual(0);
        }

        [TestCase]
        public void Add_GetAll_RoundTrip()
        {
            int id = _rewards.Add(new QuestReward { QuestId = _questId, SortOrder = 0, Description = "100 gold", IsClaimed = false });

            var list = _rewards.GetAll(_questId);
            AssertThat(list.Count).IsEqual(1);
            AssertThat(list[0].Id).IsEqual(id);
            AssertThat(list[0].Description).IsEqual("100 gold");
            AssertBool(list[0].IsClaimed).IsFalse();
        }

        [TestCase]
        public void Edit_UpdatesDescriptionAndClaimed()
        {
            var r = new QuestReward { QuestId = _questId, SortOrder = 0, Description = "50 silver", IsClaimed = false };
            r.Id = _rewards.Add(r);
            r.Description = "100 gold";
            r.IsClaimed   = true;
            _rewards.Edit(r);

            var list = _rewards.GetAll(_questId);
            AssertThat(list[0].Description).IsEqual("100 gold");
            AssertBool(list[0].IsClaimed).IsTrue();
        }

        [TestCase]
        public void Delete_RemovesRow()
        {
            int id = _rewards.Add(new QuestReward { QuestId = _questId, SortOrder = 0, Description = "Sword" });
            _rewards.Delete(id);
            AssertThat(_rewards.GetAll(_questId).Count).IsEqual(0);
        }

        [TestCase]
        public void Migrate_MigratesLegacyRewardColumn()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var p = conn.CreateCommand()) { p.CommandText = "PRAGMA foreign_keys = OFF"; p.ExecuteNonQuery(); }
            var campaigns = new CampaignRepository(conn);
            var quests    = new QuestRepository(conn);
            campaigns.Migrate();
            quests.Migrate();

            int cid = campaigns.Add(new Campaign { Name = "C2", System = "dnd5e_2024" });
            int qid = InsertQuest(conn, cid, reward: "200 gold");

            var rewards = new QuestRewardRepository(conn);
            rewards.Migrate();

            var list = rewards.GetAll(qid);
            AssertThat(list.Count).IsEqual(1);
            AssertThat(list[0].Description).IsEqual("200 gold");
            AssertBool(list[0].IsClaimed).IsFalse();

            // quests.reward column should now be cleared
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT reward FROM quests WHERE id = @id";
            checkCmd.Parameters.AddWithValue("@id", qid);
            AssertThat((string)checkCmd.ExecuteScalar()).IsEqual("");
            conn.Close();
        }

        [TestCase]
        public void Migrate_Idempotent_DoesNotDuplicateOnSecondRun()
        {
            _rewards.Migrate();
            AssertThat(_rewards.GetAll(_questId).Count).IsEqual(0);
        }
    }
}
