namespace DndBuilder.Core.Models
{
    public class FactionRelationship
    {
        public int  FactionId          { get; set; }
        public int  RelatedFactionId   { get; set; }
        public int? RelationshipTypeId { get; set; }
    }
}
