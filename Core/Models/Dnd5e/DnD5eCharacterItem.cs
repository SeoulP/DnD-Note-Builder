namespace DndBuilder.Core.Models
{
    public class DnD5eCharacterItem
    {
        public int CharacterId { get; set; }  // FK -> characters.Id
        public int ItemId      { get; set; }  // FK -> Item.Id
    }
}