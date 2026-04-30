namespace DndBuilder.Core.Models
{
    public class Encounter
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }
        public int?   SessionId  { get; set; }
        public string Name       { get; set; } = "New Encounter";
        public string StartedAt  { get; set; } = "";
        public bool   IsResolved { get; set; }
    }
}
