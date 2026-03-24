namespace DndBuilder.Core.Models
{
    public class Subclass
    {
        public int    Id          { get; set; }
        public int    CampaignId  { get; set; }
        public int    ClassId     { get; set; }
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public string Notes       { get; set; } = "";
        public int    SortOrder   { get; set; } = 0;
    }
}
