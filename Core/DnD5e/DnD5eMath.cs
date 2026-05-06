using DndBuilder.Core.Models;

namespace DndBuilder.Core
{
    public static class DnD5eMath
    {
        public static int ParseScore(string text) =>
            System.Math.Clamp(int.TryParse(text, out int v) ? v : 10, 1, 30);

        public static int AbilityMod(int score) =>
            (int)System.Math.Floor((score - 10) / 2.0);

        // Formatted as "(+N)" or "(-N)" — 5e display convention
        public static string ModLabel(int score)
        {
            int mod = AbilityMod(score);
            return mod >= 0 ? $"(+{mod})" : $"({mod})";
        }

        // +2 at level 1, increases by +1 every 4 levels
        public static int ProfBonus(int level) => 2 + (level - 1) / 4;

        public static int SkillBonus(string attr, DnD5ePlayerCharacter pc, int profBonus, bool isProficient, bool isExpertise)
        {
            int score = attr switch
            {
                "str" => pc.Strength,
                "dex" => pc.Dexterity,
                "con" => pc.Constitution,
                "int" => pc.Intelligence,
                "wis" => pc.Wisdom,
                "cha" => pc.Charisma,
                _     => 10,
            };
            int attrMod = AbilityMod(score);
            int profMod = isExpertise ? 2 * profBonus : isProficient ? profBonus : 0;
            return attrMod + profMod;
        }

        public static string SignStr(int v) => v >= 0 ? $"+{v}" : $"{v}";

        public static int ScoreFor(DnD5ePlayerCharacter pc, string attr) => attr switch
        {
            "str" => pc.Strength,
            "dex" => pc.Dexterity,
            "con" => pc.Constitution,
            "int" => pc.Intelligence,
            "wis" => pc.Wisdom,
            "cha" => pc.Charisma,
            _     => 10,
        };

        public static int SaveBonus(DnD5ePlayerCharacter pc, string attr, int profBonus)
        {
            bool prof = attr switch
            {
                "str" => pc.SaveProfStr,
                "dex" => pc.SaveProfDex,
                "con" => pc.SaveProfCon,
                "int" => pc.SaveProfInt,
                "wis" => pc.SaveProfWis,
                "cha" => pc.SaveProfCha,
                _     => false,
            };
            return AbilityMod(ScoreFor(pc, attr)) + (prof ? profBonus : 0);
        }

        public static int InitiativeBonus(DnD5ePlayerCharacter pc) =>
            AbilityMod(pc.Dexterity) + pc.InitiativeMisc;

        public static int ArmorClass(DnD5ePlayerCharacter pc) =>
            pc.ArmorClassBase + (pc.AcUseDexMod ? AbilityMod(pc.Dexterity) : 0) + pc.AcMisc;

        public static int WeaponAttackBonus(DnD5ePlayerCharacter pc, DnD5ePlayerCharacterWeapon weapon, int profBonus)
        {
            int attrMod = 0;
            if (weapon.DeriveFromAbility)
                attrMod = AbilityMod(ScoreFor(pc, ResolveWeaponAttr(pc, weapon.DeriveAbility)));
            return attrMod + (weapon.IsProficient ? profBonus : 0) + weapon.AttackBonus;
        }

        public static int WeaponDamageMod(DnD5ePlayerCharacter pc, DnD5ePlayerCharacterWeapon weapon)
        {
            int attrMod = 0;
            if (weapon.DeriveFromAbility && weapon.DeriveDamageMod)
                attrMod = AbilityMod(ScoreFor(pc, ResolveWeaponAttr(pc, weapon.DeriveAbility)));
            return attrMod + weapon.DamageBonus;
        }

        private static string ResolveWeaponAttr(DnD5ePlayerCharacter pc, string deriveAbility)
        {
            if (deriveAbility != "fin") return deriveAbility;
            return AbilityMod(pc.Strength) >= AbilityMod(pc.Dexterity) ? "str" : "dex";
        }
    }
}
