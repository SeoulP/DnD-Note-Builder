namespace DndBuilder.Core.Models
{
    public class Pf2eClass
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public int KeyAbilityScoreId { get; set; } = 0;
        public int HpPerLevel { get; set; } = 8;
        public string Description { get; set; } = "";
    }
}
