namespace DndBuilder.Core.Models
{
    public class Pf2eFeat
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public int FeatTypeId { get; set; } = 0;
        public int? ClassId { get; set; } = null;
        public int? AncestryId { get; set; } = null;
        public int LevelRequired { get; set; } = 1;
        public int ActionCostId { get; set; } = 0;
        public string Trigger { get; set; } = "";
        public string Prerequisites { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
