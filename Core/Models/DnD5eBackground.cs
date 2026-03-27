namespace DndBuilder.Core.Models
{
    public class DnD5eBackground
    {
        public int    Id            { get; set; }
        public int    CampaignId    { get; set; }
        public string Name          { get; set; } = "";
        public int    SkillCount    { get; set; } = 2;
        public string SkillNames    { get; set; } = "";  // comma-separated fixed skill names; empty = free picks
        public string Description   { get; set; } = "";
        public int?   FeatAbilityId      { get; set; }        // Origin feat granted by this background
        public string ToolOptions        { get; set; } = "";  // tool/instrument proficiency name
        public int    LanguageCount      { get; set; } = 1;   // kept for DB compat, not shown in UI
        public bool   IsCustom           { get; set; } = false;
        public string AbilityScoreOptions { get; set; } = ""; // comma-separated attr abbrevs: "str,dex,con"
    }
}
