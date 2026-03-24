namespace DndBuilder.Core.Models
{
    public class Species
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Notes       { get; set; } = "";
    }
}