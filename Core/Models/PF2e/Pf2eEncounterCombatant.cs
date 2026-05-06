namespace DndBuilder.Core.Models
{
    public class Pf2eEncounterCombatant
    {
        public int    Id               { get; set; }
        public int    EncounterId      { get; set; }
        public int?   CharacterId      { get; set; }   // set for PCs
        public int?   CreatureId       { get; set; }   // set for creatures
        public string DisplayName      { get; set; } = "";
        public int    Initiative       { get; set; }
        public int    CurrentHp        { get; set; }
        public int    MaxHp            { get; set; }
        public int    Ac               { get; set; }
        public int    SortOrder        { get; set; }
        public bool   IsActive         { get; set; } = true;
        public int    HeroPoints       { get; set; }
        public int    ActionsRemaining { get; set; } = 3;
    }
}
