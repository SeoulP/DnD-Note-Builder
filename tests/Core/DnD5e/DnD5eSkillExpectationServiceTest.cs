using GdUnit4;
using Microsoft.Data.Sqlite;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.DnD5e
{
    [TestSuite]
    public class DnD5eSkillExpectationServiceTest
    {
        private SqliteConnection             _conn;
        private CampaignRepository           _campaigns;
        private DnD5eClassRepository         _classes;
        private DnD5eAbilityRepository       _abilities;
        private DnD5eBackgroundRepository    _backgrounds;
        private DnD5eSkillExpectationService _service;
        private int                          _campaignId;

        [BeforeTest]
        public void Setup()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            _campaigns   = new CampaignRepository(_conn);
            _classes     = new DnD5eClassRepository(_conn);
            _abilities   = new DnD5eAbilityRepository(_conn);
            _backgrounds = new DnD5eBackgroundRepository(_conn);

            _campaigns.Migrate();
            _classes.Migrate();

            // abilities must exist before backgrounds.Migrate() adds feat_ability_id FK
            var createAbilities = _conn.CreateCommand();
            createAbilities.CommandText = "CREATE TABLE IF NOT EXISTS abilities (id INTEGER PRIMARY KEY, campaign_id INTEGER NOT NULL)";
            createAbilities.ExecuteNonQuery();

            _backgrounds.Migrate();

            _campaignId = _campaigns.Add(new Campaign { Name = "Test", System = "dnd5e_2024" });
            _service = new DnD5eSkillExpectationService(_classes, _abilities, _backgrounds);
        }

        [AfterTest]
        public void Teardown() => _conn?.Close();

        [TestCase]
        public void GetExpectations_NoPcData_ReturnsEmpty()
        {
            var pc = new DnD5ePlayerCharacter { CampaignId = _campaignId, Level = 1 };
            var result = _service.GetExpectations(pc);
            AssertThat(result.Count).IsEqual(0);
        }

        [TestCase]
        public void GetExpectations_WithClass_ReturnsClassExpectation()
        {
            int classId = _classes.Add(new DnD5eClass
            {
                CampaignId       = _campaignId,
                Name             = "Fighter",
                SkillChoicesCount = 2,
            });
            var pc = new DnD5ePlayerCharacter { CampaignId = _campaignId, Level = 1, ClassId = classId };

            var result = _service.GetExpectations(pc);

            AssertThat(result.Count).IsEqual(1);
            AssertThat(result[0].Source).IsEqual("class");
            AssertThat(result[0].ExpectedCount).IsEqual(2);
            AssertThat(result[0].SourceName).IsEqual("Fighter");
        }

        [TestCase]
        public void GetExpectations_WithBackground_ReturnsBackgroundExpectation()
        {
            int bgId = _backgrounds.Add(new DnD5eBackground
            {
                CampaignId = _campaignId,
                Name       = "Soldier",
                SkillCount = 2,
            });
            var pc = new DnD5ePlayerCharacter { CampaignId = _campaignId, Level = 1, BackgroundId = bgId };

            var result = _service.GetExpectations(pc);

            AssertThat(result.Count).IsEqual(1);
            AssertThat(result[0].Source).IsEqual("background");
            AssertThat(result[0].ExpectedCount).IsEqual(2);
            AssertThat(result[0].SourceName).IsEqual("Soldier");
        }

        [TestCase]
        public void GetExpectations_ClassAndBackground_ReturnsBoth()
        {
            int classId = _classes.Add(new DnD5eClass
            {
                CampaignId        = _campaignId,
                Name              = "Ranger",
                SkillChoicesCount = 3,
            });
            int bgId = _backgrounds.Add(new DnD5eBackground
            {
                CampaignId = _campaignId,
                Name       = "Outlander",
                SkillCount = 2,
            });
            var pc = new DnD5ePlayerCharacter
            {
                CampaignId   = _campaignId,
                Level        = 1,
                ClassId      = classId,
                BackgroundId = bgId,
            };

            var result = _service.GetExpectations(pc);

            AssertThat(result.Count).IsEqual(2);
        }

        [TestCase]
        public void GetExpectations_ClassWithZeroSkills_NotIncluded()
        {
            int classId = _classes.Add(new DnD5eClass
            {
                CampaignId        = _campaignId,
                Name              = "Warrior",
                SkillChoicesCount = 0,
            });
            var pc = new DnD5ePlayerCharacter { CampaignId = _campaignId, Level = 1, ClassId = classId };

            var result = _service.GetExpectations(pc);

            AssertThat(result.Count).IsEqual(0);
        }
    }
}
