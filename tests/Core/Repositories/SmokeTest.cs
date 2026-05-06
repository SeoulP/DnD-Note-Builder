using GdUnit4;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.Repositories
{
    [TestSuite]
    public class SmokeTest
    {
        private SqliteConnection _conn;
        private CampaignRepository _campaigns;

        [BeforeTest]
        public void Setup()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            _campaigns = new CampaignRepository(_conn);
            _campaigns.Migrate();
        }

        [AfterTest]
        public void Teardown()
        {
            _conn?.Close();
        }

        [TestCase]
        public void Campaign_AddAndGet_RoundTrips()
        {
            var id = _campaigns.Add(new Campaign { Name = "Test Campaign", System = "dnd5e_2024", Description = "Smoke test" });
            var loaded = _campaigns.Get(id);

            AssertThat(loaded).IsNotNull();
            AssertThat(loaded.Name).IsEqual("Test Campaign");
            AssertThat(loaded.System).IsEqual("dnd5e_2024");
        }

        [TestCase]
        public void Campaign_Edit_UpdatesName()
        {
            var id = _campaigns.Add(new Campaign { Name = "Before", System = "dnd5e_2024" });
            _campaigns.Edit(new Campaign { Id = id, Name = "After", System = "dnd5e_2024" });

            AssertThat(_campaigns.Get(id).Name).IsEqual("After");
        }

        [TestCase]
        public void Campaign_Delete_RemovesRow()
        {
            var id = _campaigns.Add(new Campaign { Name = "ToDelete", System = "dnd5e_2024" });
            _campaigns.Delete(id);

            AssertThat(_campaigns.Get(id)).IsNull();
        }
    }
}
