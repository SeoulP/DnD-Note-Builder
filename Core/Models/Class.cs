using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Class
    {
        public int    Id                  { get; set; }
        public int    CampaignId          { get; set; }
        public string Name                { get; set; } = "";
        public string Description         { get; set; } = "";
        public string Notes               { get; set; } = "";
        public int    SortOrder           { get; set; } = 0;
        public int    SubclassUnlockLevel { get; set; } = 3;

        // Core stats
        public int    HitDie              { get; set; } = 8;
        public string PrimaryAbility      { get; set; } = "";

        // Proficiencies
        public string SavingThrowProfs    { get; set; } = "";
        public string ArmorProfs          { get; set; } = "";
        public string WeaponProfs         { get; set; } = "";
        public string ToolProfs           { get; set; } = "";
        public int    SkillChoicesCount   { get; set; } = 2;
        public string SkillChoicesOptions { get; set; } = "";

        // Starting equipment
        public string StartingEquipA      { get; set; } = "";
        public string StartingEquipB      { get; set; } = "";

        // Spellcasting
        public string SpellcastingAbility { get; set; } = "";
        public bool   IsRitualCaster      { get; set; } = false;
        public bool   IsPreparedCaster    { get; set; } = false;

        // Loaded on demand
        public List<Subclass>   Subclasses { get; set; } = new();
        public List<ClassLevel> Levels     { get; set; } = new();
    }
}
