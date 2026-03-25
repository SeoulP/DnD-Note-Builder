namespace DndBuilder.Core.Models
{
    public class DnD5eBackground
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public int    SkillCount  { get; set; } = 2;
        public string SkillNames  { get; set; } = "";  // comma-separated fixed skill names; empty = free picks
        public string Description { get; set; } = "";
    }
}
