namespace DndBuilder.Core.Models
{
    public class Pf2eConditionType
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public bool HasValue { get; set; } = false;
        public string Description { get; set; } = "";
    }
}
