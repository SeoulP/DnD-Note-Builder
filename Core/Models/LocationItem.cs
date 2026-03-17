namespace DndBuilder.Core.Models
{
    public class LocationItem
    {
        public int LocationId { get; set; }  // FK -> Location.Id
        public int ItemId     { get; set; }  // FK -> Item.Id
    }
}