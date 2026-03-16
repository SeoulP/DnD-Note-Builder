namespace DndBuilder.Core.Models
{
    public class Location
    {
        public int    Id               { get; set; }
        public int    CampaignId       { get; set; }
        public string Name             { get; set; } = "";
        public string Type             { get; set; } = "";   // e.g. "Town", "Dungeon", "Inn", "Castle"
        public string Description      { get; set; } = "";
        public string Notes            { get; set; } = "";   // DM-facing secrets
        public string MapRef           { get; set; } = "";   // e.g. "#6 on town map"
        public int?   ParentLocationId { get; set; }         // Null = top-level location
    }
}
