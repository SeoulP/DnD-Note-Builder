namespace DndBuilder.Core.Models
{
    public class EntityAlias
    {
        public int    Id         { get; set; }
        public int    CampaignId { get; set; }
        public string EntityType { get; set; } = "";
        public int    EntityId   { get; set; }
        public string Alias      { get; set; } = "";
    }
}
