namespace DndBuilder.Core.Models
{
    public class Session
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }
        public int    Number     { get; set; }   // e.g. 7 — displayed as "Session 007"
        public string Title      { get; set; } = "";
        public string Notes      { get; set; } = "";
        public string PlayedOn   { get; set; } = "";   // ISO-8601 date string, matches your DateStarted pattern
    }
}
