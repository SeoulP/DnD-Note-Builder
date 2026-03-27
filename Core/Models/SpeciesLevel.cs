namespace DndBuilder.Core.Models
{
    public class SpeciesLevel
    {
        public int    Id        { get; set; }
        public int    SpeciesId { get; set; }
        public int    Level     { get; set; }
        public string Features  { get; set; } = "";
        public string ClassData { get; set; } = "";
    }
}
