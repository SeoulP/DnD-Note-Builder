namespace DndBuilder.Core.Models
{
    public class Pf2eAncestryFeature
    {
        public int Id { get; set; } = 0;
        public int AncestryId { get; set; } = 0;
        public int Level { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
