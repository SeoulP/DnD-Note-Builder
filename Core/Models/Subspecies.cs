namespace DndBuilder.Core.Models
{
    public class Subspecies
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public int    SpeciesId   { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Notes       { get; set; } = "";
    }
}
