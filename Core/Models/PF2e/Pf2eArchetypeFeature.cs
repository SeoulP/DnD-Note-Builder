namespace DndBuilder.Core.Models
{
    public class Pf2eArchetypeFeature
    {
        public int Id { get; set; } = 0;
        public int ArchetypeId { get; set; } = 0;
        public int Level { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
