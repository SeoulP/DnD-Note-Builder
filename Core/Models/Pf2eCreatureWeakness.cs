namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureWeakness
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int DamageTypeId { get; set; } = 0;
        public int Value { get; set; } = 0;
    }
}
