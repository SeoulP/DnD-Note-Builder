using System;
using GdUnit4;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.Repositories
{
    [TestSuite]
    public class SessionRepositoryTest
    {
        private SqliteConnection _conn;
        private CampaignRepository _campaigns;
        private SessionRepository _sessions;
        private int _campaignId;

        [BeforeTest]
        public void Setup()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            _campaigns = new CampaignRepository(_conn);
            _campaigns.Migrate();
            _sessions = new SessionRepository(_conn);
            _sessions.Migrate();
            _campaignId = _campaigns.Add(new Campaign { Name = "Test", System = "dnd5e_2024" });
        }

        [AfterTest]
        public void Teardown()
        {
            _conn?.Close();
        }

        [TestCase]
        public void Add_WithEmptyPlayedOn_DefaultsToToday()
        {
            var id = _sessions.Add(new Session { CampaignId = _campaignId, Number = 1, PlayedOn = "" });
            var loaded = _sessions.Get(id);

            AssertThat(loaded.PlayedOn).IsEqual(DateTime.Today.ToString("yyyy-MM-dd"));
        }

        [TestCase]
        public void Add_WithNullPlayedOn_DefaultsToToday()
        {
            var id = _sessions.Add(new Session { CampaignId = _campaignId, Number = 2, PlayedOn = null });
            var loaded = _sessions.Get(id);

            AssertThat(loaded.PlayedOn).IsEqual(DateTime.Today.ToString("yyyy-MM-dd"));
        }

        [TestCase]
        public void Add_WithExplicitDate_PreservesIt()
        {
            var id = _sessions.Add(new Session { CampaignId = _campaignId, Number = 3, PlayedOn = "2025-12-31" });
            var loaded = _sessions.Get(id);

            AssertThat(loaded.PlayedOn).IsEqual("2025-12-31");
        }

        [TestCase]
        public void Add_AndGet_RoundTripsAllFields()
        {
            var id = _sessions.Add(new Session
            {
                CampaignId = _campaignId,
                Number     = 4,
                Title      = "The Dragon Awakens",
                Notes      = "Party defeated the kobolds.",
                PlayedOn   = "2026-01-15",
            });
            var loaded = _sessions.Get(id);

            AssertThat(loaded).IsNotNull();
            AssertThat(loaded.CampaignId).IsEqual(_campaignId);
            AssertThat(loaded.Number).IsEqual(4);
            AssertThat(loaded.Title).IsEqual("The Dragon Awakens");
            AssertThat(loaded.Notes).IsEqual("Party defeated the kobolds.");
            AssertThat(loaded.PlayedOn).IsEqual("2026-01-15");
        }
    }
}
