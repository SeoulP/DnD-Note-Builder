namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterAttack
    {
        public int Id { get; set; } = 0;
        public int CharacterId { get; set; } = 0;
        public int AttackCategoryId { get; set; } = 0;
        public int ProficiencyRankId { get; set; } = 0;
    }
}
