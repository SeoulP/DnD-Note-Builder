namespace DndBuilder.Core.Models
{
    public class Pf2eAncestry
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public int BaseHp { get; set; } = 8;
        public int SizeId { get; set; } = 0;
        public int SpeedFeet { get; set; } = 25;
        public string Description { get; set; } = "";
    }
}
