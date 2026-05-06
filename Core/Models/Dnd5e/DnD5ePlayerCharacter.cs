using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class DnD5ePlayerCharacter : Character
    {
        public int?              ClassId      { get; set; }
        public int?              SubclassId   { get; set; }
        public int?              SubspeciesId { get; set; }
        public int?              BackgroundId { get; set; }
        public int               Level        { get; set; } = 1;
        public int               Strength     { get; set; } = 10;
        public int               Dexterity    { get; set; } = 10;
        public int               Constitution { get; set; } = 10;
        public int               Intelligence { get; set; } = 10;
        public int               Wisdom       { get; set; } = 10;
        public int               Charisma     { get; set; } = 10;
        public string            BackgroundAsi { get; set; } = ""; // e.g. "str:2,dex:1" or "int:1,wis:1,cha:1"
        public List<DnD5eCharacterAbility> Abilities { get; set; } = new();

        // HP
        public int CurrentHp      { get; set; }
        public int MaxHp          { get; set; }
        public int TempHp         { get; set; }

        // Initiative
        public int InitiativeMisc { get; set; }

        // Armor Class
        public int  ArmorClassBase { get; set; } = 10;
        public bool AcUseDexMod   { get; set; } = true;
        public int  AcMisc        { get; set; }

        // Speed / Inspiration
        public int  Speed       { get; set; } = 30;
        public bool Inspiration { get; set; }

        // Death saves
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures  { get; set; }

        // Inventory notes
        public string InventoryNotes { get; set; } = "";

        // Saving throw proficiencies
        public bool SaveProfStr   { get; set; }
        public bool SaveProfDex   { get; set; }
        public bool SaveProfCon   { get; set; }
        public bool SaveProfInt   { get; set; }
        public bool SaveProfWis   { get; set; }
        public bool SaveProfCha   { get; set; }
    }
}