namespace DndBuilder.Core.Models
{
    public class Pf2eCharacter
    {
        public int    Id            { get; set; } = 0;
        public int    CampaignId    { get; set; } = 0;
        public string Name          { get; set; } = "";
        public string Description   { get; set; } = "";
        public string Notes         { get; set; } = "";
        public int?   AncestryId    { get; set; } = null;
        public int?   HeritageId    { get; set; } = null;
        public int?   BackgroundId  { get; set; } = null;
        public int?   ClassId       { get; set; } = null;
        public int?   ArchetypeId   { get; set; } = null;
        public int    Level         { get; set; } = 1;
        public int    Strength      { get; set; } = 10;
        public int    Dexterity     { get; set; } = 10;
        public int    Constitution  { get; set; } = 10;
        public int    Intelligence  { get; set; } = 10;
        public int    Wisdom        { get; set; } = 10;
        public int    Charisma      { get; set; } = 10;
        public int    MaxHp         { get; set; } = 0;
        public int    CurrentHp     { get; set; } = 0;
        public int    HeroPoints    { get; set; } = 1;
        public int    Ac            { get; set; } = 10;
        public int    SpeedFeet     { get; set; } = 25;
        public int    FortitudeRank { get; set; } = 0;
        public int    ReflexRank    { get; set; } = 0;
        public int    WillRank      { get; set; } = 0;
        public int    PerceptionRank { get; set; } = 0;
    }
}
