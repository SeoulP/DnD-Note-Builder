namespace DndBuilder.Core.Models
{
    public class Pf2eCreatureSpeed
    {
        public int Id { get; set; } = 0;
        public int CreatureId { get; set; } = 0;
        public int MovementTypeId { get; set; } = 0;
        public int SpeedFeet { get; set; } = 25;
        public string Notes { get; set; } = "";
    }
}
