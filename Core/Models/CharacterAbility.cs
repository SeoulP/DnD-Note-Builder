namespace DndBuilder.Core.Models
{
    public class CharacterAbility
    {
        public int    CharacterId { get; set; }
        public int    AbilityId   { get; set; }
        public string Source      { get; set; } = "auto"; // "auto" or "manual"
    }
}
