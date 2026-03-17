namespace DndBuilder.Core.Models
{
    public class Faction
    {
        public int    Id           { get; set; }
        public int    CampaignId   { get; set; }
        public string Name         { get; set; } = "";
        public string Type         { get; set; } = "";   // e.g. "Merchant Guild", "Bandit Gang"
        public string Description  { get; set; } = "";
        public string Notes        { get; set; } = "";   // DM secrets
        public string Goals        { get; set; } = "";
        public int    Reputation   { get; set; } = 0;   // Negative = hostile, positive = allied
    }
}
