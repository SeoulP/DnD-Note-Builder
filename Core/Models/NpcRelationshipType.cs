namespace DndBuilder.Core.Models
{
    public class NpcRelationshipType
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
    }
}