namespace DndBuilder.Core.Models
{
    public class EntityImage
    {
        public int        Id         { get; set; }
        public EntityType EntityType { get; set; }
        public int        EntityId   { get; set; }
        public string     Path       { get; set; } = "";
        public int        SortOrder  { get; set; } = 0;
    }
}