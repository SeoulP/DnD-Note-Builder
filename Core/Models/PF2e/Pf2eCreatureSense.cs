namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureSense
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int SenseTypeId { get; set; } = 0;
        public bool IsPrecise { get; set; } = true;
        public int? RangeFeet { get; set; } = null;
        public string Notes { get; set; } = "";
    }
}
