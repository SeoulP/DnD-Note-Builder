namespace DndBuilder.Core.Models
{
    public class DnD5ePlayerCharacterWeapon
    {
        public int    Id                { get; set; }
        public int    PlayerCharacterId { get; set; }
        public int    SortOrder         { get; set; }
        public string Name              { get; set; } = "";
        public string DamageDice        { get; set; } = "";
        public string DamageType        { get; set; } = "";
        public string Notes             { get; set; } = "";
        public bool   DeriveFromAbility { get; set; } = true;
        public string DeriveAbility     { get; set; } = "str"; // "str" / "dex" / "fin"
        public bool   IsProficient      { get; set; }
        public bool   DeriveDamageMod   { get; set; } = true;
        public int    AttackBonus       { get; set; }
        public int    DamageBonus       { get; set; }
    }
}
