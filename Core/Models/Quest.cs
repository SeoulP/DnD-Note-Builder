using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Quest
    {
        public int    Id             { get; set; }
        public int    CampaignId     { get; set; }
        public string Name           { get; set; } = "";
        public int?   StatusId       { get; set; }  // FK -> QuestStatus.Id
        public string Description    { get; set; } = "";
        public string Notes          { get; set; } = "";  // DM secrets
        public int?   QuestGiverId   { get; set; }  // FK -> characters.Id (optional)
        public int?   LocationId     { get; set; }  // FK -> locations.Id (optional)
        public string Reward         { get; set; } = "";

        // Loaded on demand; not a DB column
        public List<QuestHistory> History { get; set; } = new();
    }
}
