using System;

public static class SidebarSearch
{
    private const float FuzzyThreshold = 0.7f;

    public static bool FuzzyMatch(string query, string target)
    {
        if (string.IsNullOrEmpty(query)) return true;
        query  = query.ToLowerInvariant();
        target = target.ToLowerInvariant();
        if (target.Contains(query)) return true;
        if (IsSubsequence(query, target)) return true;
        int qLen = query.Length, tLen = target.Length, windowLen = qLen;
        if (windowLen <= tLen)
        {
            for (int start = 0; start <= tLen - windowLen; start++)
                if (Similarity(query, target.Substring(start, windowLen)) >= FuzzyThreshold) return true;
        }
        else if (Similarity(query, target) >= FuzzyThreshold) return true;
        return false;
    }

    private static bool IsSubsequence(string query, string target)
    {
        int qi = 0;
        for (int i = 0; i < target.Length && qi < query.Length; i++)
            if (target[i] == query[qi]) qi++;
        return qi == query.Length;
    }

    private static float Similarity(string a, string b)
    {
        int dist = LevenshteinDistance(a, b);
        return 1f - (float)dist / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int aLen = a.Length, bLen = b.Length;
        var dp = new int[aLen + 1, bLen + 1];
        for (int i = 0; i <= aLen; i++) dp[i, 0] = i;
        for (int j = 0; j <= bLen; j++) dp[0, j] = j;
        for (int i = 1; i <= aLen; i++)
            for (int j = 1; j <= bLen; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
        return dp[aLen, bLen];
    }
}
