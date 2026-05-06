namespace DndBuilder.Core.Models
{
    public class DnD5eCharacterRelationship
    {
        public int  CharacterId          { get; set; }
        public int  RelatedCharacterId   { get; set; }
        public int? RelationshipTypeId   { get; set; }
    }
}
