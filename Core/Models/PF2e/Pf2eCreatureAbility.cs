namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureAbility
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int AbilityTypeId { get; set; } = 0;
        public int ActionCostId { get; set; } = 0;
        public string Name { get; set; } = "";
        public string Trigger { get; set; } = "";
        public int? IsMelee { get; set; } = null;
        public int? AttackBonus { get; set; } = null;
        public int? AttackBonus2 { get; set; } = null;
        public int? AttackBonus3 { get; set; } = null;
        public int? AreaTypeId { get; set; } = null;
        public int? AreaSizeFeet { get; set; } = null;
        public int? RangeFeet { get; set; } = null;
        public int? TraditionId { get; set; } = null;
        public int? SpellDc { get; set; } = null;
        public int? SpellAttack { get; set; } = null;
        public string EffectText { get; set; } = "";
        public int SortOrder { get; set; } = 0;
    }
}
