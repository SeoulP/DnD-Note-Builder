namespace DndBuilder.Core.Models
{
    public class DnD5eCharacterSkill
    {
        public int    Id                { get; set; }
        public int    PlayerCharacterId { get; set; }
        public int    SkillId           { get; set; }
        public string Source            { get; set; } = "custom";  // "class" | "background" | "feat" | "custom"
        public int?   SourceId          { get; set; }
        public bool   IsExpertise       { get; set; } = false;
    }
}
