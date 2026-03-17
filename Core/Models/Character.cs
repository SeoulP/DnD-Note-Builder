using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Character
    {
        public int       Id           { get; set; }
        public int       CampaignId   { get; set; }
        public string    Name         { get; set; } = "";
        public string    PortraitPath { get; set; } = "";
        public string    Gender       { get; set; } = "";
        public string    Occupation   { get; set; } = "";
        public string    Description  { get; set; } = "";  // Appearance / first impression
        public string    Personality  { get; set; } = "";
        public string    Notes        { get; set; } = "";
        public int?      SpeciesId    { get; set; }
        public List<int> FactionIds   { get; set; } = new();
    }
}