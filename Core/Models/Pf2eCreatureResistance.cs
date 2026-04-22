namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureResistance
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int DamageTypeId { get; set; } = 0;
        public int Value { get; set; } = 0;
        public string ExceptionNote { get; set; } = "";
    }
}
