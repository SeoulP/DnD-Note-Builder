namespace DndBuilder.Core.Models
{
    public class Pf2eArchetype
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public int ClassId { get; set; } = 0;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
