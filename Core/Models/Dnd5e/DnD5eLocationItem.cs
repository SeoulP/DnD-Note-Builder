namespace DndBuilder.Core.Models
{
    public class DnD5eLocationItem
    {
        public int LocationId { get; set; }  // FK -> Location.Id
        public int ItemId     { get; set; }  // FK -> DnD5eItem.Id
    }
}