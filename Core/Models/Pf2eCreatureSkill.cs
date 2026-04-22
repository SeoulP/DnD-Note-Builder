namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureSkill
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int SkillTypeId { get; set; } = 0;
        public int Modifier { get; set; } = 0;
        public string Notes { get; set; } = "";
    }
}
