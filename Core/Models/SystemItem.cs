namespace DndBuilder.Core.Models
{
    public class SystemItem
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }  // Direct campaign scope for fast queries
        public int    ItemId     { get; set; }  // FK -> Item.Id
        public string System     { get; set; } = "";  // e.g. "dnd5e_2024"
    }
}