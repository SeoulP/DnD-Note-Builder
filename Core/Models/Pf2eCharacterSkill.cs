namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterSkill
    {
        public int Id { get; set; } = 0;
        public int CharacterId { get; set; } = 0;
        public int SkillTypeId { get; set; } = 0;
        public int ProficiencyRankId { get; set; } = 0;
    }
}
