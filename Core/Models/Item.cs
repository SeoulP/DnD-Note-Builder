namespace DndBuilder.Core.Models
{
    public class Item
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Notes       { get; set; } = "";  // DM secrets / history
        public bool   IsUnique    { get; set; } = false;
        public int?   TypeId      { get; set; }        // FK -> ItemType.Id
    }
}