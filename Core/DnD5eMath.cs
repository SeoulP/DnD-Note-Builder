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

        public static int SkillBonus(string attr, PlayerCharacter pc, int profBonus, bool isProficient, bool isExpertise)
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
    }
}
