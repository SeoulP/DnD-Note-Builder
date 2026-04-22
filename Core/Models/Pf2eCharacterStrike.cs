namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterStrike
    {
        public int Id { get; set; } = 0;
        public int CharacterId { get; set; } = 0;
        public string Name { get; set; } = "";
        public bool IsMelee { get; set; } = true;
        public int AttackBonus { get; set; } = 0;
        public int AttackBonus2 { get; set; } = 0;
        public int AttackBonus3 { get; set; } = 0;
        public int? AreaTypeId { get; set; } = null;
        public int? AreaSizeFeet { get; set; } = null;
        public int? RangeFeet { get; set; } = null;
        public string Notes { get; set; } = "";
        public int SortOrder { get; set; } = 0;
    }
}
