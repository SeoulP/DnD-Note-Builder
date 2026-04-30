using System;

namespace DndBuilder.Core
{
    /// <summary>
    /// Parses and evaluates ability-uses formula strings stored in class/species level progression.
    ///
    /// Format: "base|level|prof|attr"
    ///   base  = integer (0+)
    ///   level = "" | "full" | "half" | "ceil" | "double"
    ///   prof  = "" | "prof"
    ///   attr  = "" | "str" | "dex" | "con" | "int" | "wis" | "cha"
    ///
    /// Special values:
    ///   "--"           = no uses specified (returns 0)
    ///   plain integer  = flat base, no modifiers (backward-compatible)
    /// </summary>
    public static class UsesFormula
    {
        public static int Evaluate(string formula, int charLevel, int str, int dex, int con, int intel, int wis, int cha)
        {
            if (string.IsNullOrEmpty(formula) || formula == "--") return 0;
            if (int.TryParse(formula, out int flat)) return flat;

            var p = formula.Split('|');
            if (p.Length < 4) return 0;

            int.TryParse(p[0], out int baseVal);

            int result = baseVal;

            result += p[1] switch
            {
                "full"   => charLevel,
                "half"   => charLevel / 2,
                "ceil"   => (charLevel + 1) / 2,
                "double" => charLevel * 2,
                _        => 0,
            };

            if (p[2] == "prof") result += DnD5eMath.ProfBonus(charLevel);

            result += p[3] switch
            {
                "str" => DnD5eMath.AbilityMod(str),
                "dex" => DnD5eMath.AbilityMod(dex),
                "con" => DnD5eMath.AbilityMod(con),
                "int" => DnD5eMath.AbilityMod(intel),
                "wis" => DnD5eMath.AbilityMod(wis),
                "cha" => DnD5eMath.AbilityMod(cha),
                _     => 0,
            };

            return Math.Max(0, result);
        }

        public static string FormatForDisplay(string formula)
        {
            if (string.IsNullOrEmpty(formula) || formula == "--") return "--";
            if (int.TryParse(formula, out int flat)) return flat.ToString();

            var p = formula.Split('|');
            if (p.Length < 4) return formula;

            var sb = new System.Text.StringBuilder();

            if (int.TryParse(p[0], out int b) && b > 0) sb.Append(b);

            if (p[1] != "")
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append(p[1] switch
                {
                    "full"   => "Lvl",
                    "half"   => "½Lvl↓",
                    "ceil"   => "½Lvl↑",
                    "double" => "Lvl×2",
                    _        => p[1],
                });
            }

            if (p[2] == "prof") { if (sb.Length > 0) sb.Append("+"); sb.Append("Prof"); }

            if (p[3] != "")
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append(char.ToUpper(p[3][0]) + p[3][1..] + "Mod");
            }

            return sb.Length > 0 ? sb.ToString() : "0";
        }

    }
}
