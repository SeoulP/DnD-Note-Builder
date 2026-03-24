using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class Ability
    {
        public int    Id              { get; set; }
        public int    CampaignId      { get; set; }
        public string Name            { get; set; } = "";
        public string Type            { get; set; } = "";  // legacy free-text, kept for DB compatibility
        public int?   TypeId          { get; set; }        // FK → ability_types
        public string Action            { get; set; } = "";  // Action / Bonus Action / Reaction / Passive / Free
        public string Trigger           { get; set; } = "";
        public string Recovery          { get; set; } = "";  // legacy free-text, kept for DB compatibility
        public string RecoveryInterval  { get; set; } = "";  // ShortRest / LongRest / ShortOrLongRest / None
        public int    RecoveryAmount    { get; set; } = 0;   // 0 = Max, positive = fixed count
        public string Effect            { get; set; } = "";
        public string Notes           { get; set; } = "";
        public int    SortOrder       { get; set; } = 0;

        // Choices
        public int    MaxChoices             { get; set; } = 0;
        public string ChoicePoolType         { get; set; } = "";  // "" = none, "fixed", "weapon", "skill", "custom"

        // How pick count is determined: "formula" | "progression"
        public string PickCountMode          { get; set; } = "formula";

        // Structured pick-count formula: Math.Max(0, Base + attr_mod + prof + level)
        public int    ChoiceCountBase        { get; set; } = 0;
        public string ChoiceCountAttribute   { get; set; } = "";  // "" | "str" | "dex" | "con" | "int" | "wis" | "cha"
        public bool   ChoiceCountAddProf     { get; set; } = false;
        public string ChoiceCountAddLevel    { get; set; } = "";  // "" | "full" | "half_down" | "half_up"

        // Loaded on demand
        public List<AbilityCost>            Costs            { get; set; } = new();
        public List<AbilityChoice>          Choices          { get; set; } = new();
        public List<AbilityChoiceProgression> ChoiceProgression { get; set; } = new();
    }
}
