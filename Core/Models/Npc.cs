using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Npc
    {
        public int             Id               { get; set; }
        public int             CampaignId       { get; set; }
        public string          Name             { get; set; } = "";
        public int?            SpeciesId        { get; set; }
        public string          Gender           { get; set; } = "";
        public string          Occupation       { get; set; } = "";
        public string          Description      { get; set; } = "";   // Appearance / first impression
        public string          Personality      { get; set; } = "";
        public string          Notes            { get; set; } = "";   // DM secrets / plot hooks
        public string          PortraitPath     { get; set; } = "";
        public NpcStatus       Status           { get; set; } = NpcStatus.Unknown;
        public NpcRelationship Relationship     { get; set; } = NpcRelationship.Neutral;
        public List<int>       FactionIds       { get; set; } = new();
        public int?            HomeLocationId   { get; set; }         // "Usually found at" — optional soft link
        public int?            FirstSeenSession { get; set; }
    }
}