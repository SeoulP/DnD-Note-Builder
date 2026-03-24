using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class PlayerCharacter : Character
    {
        public int?              ClassId      { get; set; }
        public int?              SubclassId   { get; set; }
        public int?              SubspeciesId { get; set; }
        public int               Level        { get; set; } = 1;
        public int               Strength     { get; set; } = 10;
        public int               Dexterity    { get; set; } = 10;
        public int               Constitution { get; set; } = 10;
        public int               Intelligence { get; set; } = 10;
        public int               Wisdom       { get; set; } = 10;
        public int               Charisma     { get; set; } = 10;
        public List<CharacterAbility> Abilities { get; set; } = new();
    }
}