namespace DndBuilder.Core.Models
{
    public class Pf2eEncounterCombatantHpLogEntry
    {
        public int    Id          { get; set; }
        public int    CombatantId { get; set; }
        public int    Delta       { get; set; }
        public string ReasonText  { get; set; } = "";
        public string LoggedAt    { get; set; } = "";
    }
}
