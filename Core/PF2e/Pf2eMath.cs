namespace DndBuilder.Core
{
    public static class Pf2eMath
    {
        public static int ParseScore(string text) =>
            System.Math.Clamp(int.TryParse(text, out int v) ? v : 10, 1, 30);

        public static int AbilityMod(int score) =>
            (int)System.Math.Floor((score - 10) / 2.0);

        // Untrained (rank 0) = 0; otherwise level + rank * 2
        public static int ProfBonus(int rankValue, int level) =>
            rankValue > 0 ? level + rankValue * 2 : 0;

        public static string SignStr(int v) => v >= 0 ? $"+{v}" : $"{v}";

        public static string ModStr(int score) => SignStr(AbilityMod(score));
    }
}
