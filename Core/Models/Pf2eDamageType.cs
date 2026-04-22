namespace DndBuilder.Core.Models
{
    public class Pf2eDamageType
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public bool IsPhysical { get; set; } = false;
        public bool IsEnergy { get; set; } = false;
        public bool IsPersistent { get; set; } = false;
        public bool IsSplash { get; set; } = false;
    }
}
