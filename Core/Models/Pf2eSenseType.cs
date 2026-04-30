namespace DndBuilder.Core.Models
{
    public class Pf2eSenseType
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public bool IsPrecise { get; set; } = false;
        public string Description { get; set; } = "";
    }
}
