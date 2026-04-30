namespace DndBuilder.Core.Models
{
    public class Pf2eEncounterCombatantCondition
    {
        public int Id               { get; set; }
        public int CombatantId      { get; set; }
        public int ConditionTypeId  { get; set; }
        public int ConditionValue   { get; set; }
    }
}
