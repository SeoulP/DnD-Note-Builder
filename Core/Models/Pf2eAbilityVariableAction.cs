namespace DndBuilder.Core.Models
{
    public class Pf2eAbilityVariableAction
    {
        public int Id { get; set; } = 0;
        public int AbilityId { get; set; } = 0;
        public int ActionCostId { get; set; } = 0;
        public string EffectText { get; set; } = "";
        public int? DiceCount { get; set; } = null;
        public int? DieTypeId { get; set; } = null;
        public int? Bonus { get; set; } = null;
        public int? DamageTypeId { get; set; } = null;
        public int? SaveTypeId { get; set; } = null;
        public int? SaveDc { get; set; } = null;
        public int? AreaTypeId { get; set; } = null;
        public int? AreaSizeFeet { get; set; } = null;
        public int? RangeFeet { get; set; } = null;
    }
}
