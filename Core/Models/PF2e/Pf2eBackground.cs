namespace DndBuilder.Core.Models
{
    public class Pf2eBackground
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int? Skill1Id { get; set; } = null;
        public int? Skill2Id { get; set; } = null;
        public string LoreSkillName { get; set; } = "";
        public int? GrantedFeatId { get; set; } = null;
    }
}
