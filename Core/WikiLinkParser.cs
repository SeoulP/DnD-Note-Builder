using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class WikiLinkParser
{
    private static readonly Regex LinkPattern = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);

    public static string Parse(string text, DatabaseService db, int campaignId)
    {
        var lookup = BuildLookup(db, campaignId);
        var sb = new StringBuilder();
        int last = 0;

        foreach (Match m in LinkPattern.Matches(text))
        {
            sb.Append(text[last..m.Index]);
            var name = m.Groups[1].Value;
            sb.Append(lookup.TryGetValue(name.ToLowerInvariant(), out var url)
                ? $"[url={url}][color=#d4aa70]{name}[/color][/url]"
                : $"[color=#888888][[{name}]][/color]");
            last = m.Index + m.Length;
        }

        sb.Append(text[last..]);
        return sb.ToString();
    }

    private static Dictionary<string, string> BuildLookup(DatabaseService db, int campaignId)
    {
        var d = new Dictionary<string, string>();
        foreach (var x in db.Npcs.GetAll(campaignId))      d[x.Name.ToLowerInvariant()]  = $"npc:{x.Id}";
        foreach (var x in db.Factions.GetAll(campaignId))  d[x.Name.ToLowerInvariant()]  = $"faction:{x.Id}";
        foreach (var x in db.Locations.GetAll(campaignId)) d[x.Name.ToLowerInvariant()]  = $"location:{x.Id}";
        foreach (var x in db.Sessions.GetAll(campaignId))  d[x.Title.ToLowerInvariant()] = $"session:{x.Id}";
        foreach (var x in db.Items.GetAll(campaignId))     d[x.Name.ToLowerInvariant()]  = $"item:{x.Id}";
        foreach (var x in db.Quests.GetAll(campaignId))    d[x.Name.ToLowerInvariant()]  = $"quest:{x.Id}";
        // Aliases resolve to the same URL as the entity; entity name takes precedence on conflict
        foreach (var a in db.EntityAliases.GetAll(campaignId))
        {
            string key = a.Alias.ToLowerInvariant();
            if (!d.ContainsKey(key))
                d[key] = $"{a.EntityType}:{a.EntityId}";
        }
        return d;
    }
}