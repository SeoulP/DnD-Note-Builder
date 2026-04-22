namespace DndBuilder.Core.Models
{
    public class Pf2eInnateSpell
    {
        public int Id { get; set; } = 0;
        public int AbilityId { get; set; } = 0;
        public string SpellName { get; set; } = "";
        public int SpellRank { get; set; } = 0;
        public int SpellFrequencyId { get; set; } = 0;
        public string Notes { get; set; } = "";
    }
}
