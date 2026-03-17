namespace DndBuilder.Core.Models
{
    public class Npc : Character
    {
        public int? HomeLocationId      { get; set; }
        public int? FirstSeenSession    { get; set; }
        public int? RelationshipTypeId  { get; set; }  // FK -> NpcRelationshipType.Id
        public int? StatusId            { get; set; }  // FK -> NpcStatus.Id
    }
}