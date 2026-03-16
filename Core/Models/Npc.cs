namespace DndBuilder.Core.Models
{
    public class Npc
    {
        public int    Id               { get; set; }
        public int    CampaignId       { get; set; }
        public string Name             { get; set; } = "";
        public string Species          { get; set; } = "";
        public string Gender           { get; set; } = "";
        public string Occupation       { get; set; } = "";
        public string Description      { get; set; } = "";   // Appearance / first impression
        public string Personality      { get; set; } = "";
        public string Notes            { get; set; } = "";   // DM secrets / plot hooks
        public string PortraitPath     { get; set; } = "";
        public int    Status           { get; set; } = 0;   // 0=Unknown 1=Alive 2=Dead 3=Missing 4=Captured
        public int    Relationship     { get; set; } = 2;   // 0=Hostile 1=Unfriendly 2=Neutral 3=Friendly 4=Allied
        public int?   FactionId        { get; set; }
        public int?   LocationId       { get; set; }
        public int?   FirstSeenSession { get; set; }
    }
}
