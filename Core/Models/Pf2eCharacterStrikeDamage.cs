namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterStrikeDamage
    {
        public int Id { get; set; } = 0;
        public int StrikeId { get; set; } = 0;
        public int DamageTypeId { get; set; } = 0;
        public int DiceCount { get; set; } = 1;
        public int DieTypeId { get; set; } = 0;
        public int Bonus { get; set; } = 0;
        public bool IsPrimary { get; set; } = true;
        public string Notes { get; set; } = "";
    }
}
