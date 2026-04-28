using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace DndBuilder.Core.Seeding
{
    public static class JsonSeedLoader
    {
        public static List<JsonElement> Load(string relativePath)
        {
            string fullPath = ProjectSettings.GlobalizePath($"res://Data/{relativePath}");
            if (!File.Exists(fullPath))
            {
                AppLogger.Instance.Warn("JsonSeedLoader", $"Seed file not found: {fullPath}");
                return new List<JsonElement>();
            }
            string json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<List<JsonElement>>(json);
        }
    }
}
