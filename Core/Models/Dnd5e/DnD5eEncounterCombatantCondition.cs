namespace DndBuilder.Core.Models
{
    public class DnD5eEncounterCombatantCondition
    {
        public int Id              { get; set; }
        public int CombatantId     { get; set; }
        public int ConditionTypeId { get; set; }
    }
}
