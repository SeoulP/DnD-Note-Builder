namespace DndBuilder.Core.Models
{
    public class CharacterItem
    {
        public int CharacterId { get; set; }  // FK -> characters.Id
        public int ItemId      { get; set; }  // FK -> Item.Id
    }
}