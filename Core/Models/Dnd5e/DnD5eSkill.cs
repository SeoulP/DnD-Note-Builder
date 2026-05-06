namespace DndBuilder.Core.Models
{
    public class DnD5eSkill
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }
        public string Name       { get; set; } = "";
        public string Attribute  { get; set; } = "";  // "str" | "dex" | "con" | "int" | "wis" | "cha"
    }
}
