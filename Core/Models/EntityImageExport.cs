namespace DndBuilder.Core.Models
{
    public class EntityImageExport
    {
        public EntityType EntityType  { get; set; }
        public int        OldEntityId { get; set; }
        public string     Extension   { get; set; } = ".png";
        public string     DataBase64  { get; set; } = "";
        public int        SortOrder   { get; set; }
    }
}
