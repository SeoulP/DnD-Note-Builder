namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureImmunity
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int? DamageTypeId { get; set; } = null;
        public int? ConditionTypeId { get; set; } = null;
        public string Notes { get; set; } = "";
    }
}
