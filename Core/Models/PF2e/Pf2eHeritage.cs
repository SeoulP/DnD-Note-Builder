namespace DndBuilder.Core.Models
{
    public class Pf2eHeritage
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public int AncestryId { get; set; } = 0;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
