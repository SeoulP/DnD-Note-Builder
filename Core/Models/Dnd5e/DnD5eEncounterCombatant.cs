namespace DndBuilder.Core.Models
{
    public class DnD5eEncounterCombatant
    {
        public int    Id                         { get; set; }
        public int    EncounterId                { get; set; }
        public int?   CharacterId                { get; set; }
        public int?   CreatureId                 { get; set; }
        public string DisplayName                { get; set; } = "";
        public int    Initiative                  { get; set; }
        public int    CurrentHp                  { get; set; }
        public int    MaxHp                      { get; set; }
        public int    TempHp                     { get; set; }
        public int    SortOrder                  { get; set; }
        public bool   IsActive                   { get; set; } = true;
        public string ConcentrationSpell         { get; set; } = "";
        public int    DeathSaveSuccesses         { get; set; }
        public int    DeathSaveFailures          { get; set; }
        public int    LegendaryActionsRemaining  { get; set; }
        public int    LegendaryActionsMax        { get; set; }
    }
}
