namespace DndBuilder.Core.Models
{
    public class CharacterRelationship
    {
        public int  CharacterId          { get; set; }
        public int  RelatedCharacterId   { get; set; }
        public int? RelationshipTypeId   { get; set; }
    }
}
