using System;
using System.Collections.Generic;

namespace DndBuilder.Core.Models
{
    public class ExportPackage
    {
        public int    Version    { get; set; } = 1;
        public string ExportedAt { get; set; } = DateTime.UtcNow.ToString("o");

        // Seeded types
        public List<Species>                   Species                   { get; set; } = new();
        public List<NpcStatus>                 NpcStatuses               { get; set; } = new();
        public List<NpcRelationshipType>       NpcRelationshipTypes      { get; set; } = new();
        public List<NpcFactionRole>            NpcFactionRoles           { get; set; } = new();
        public List<CharacterRelationshipType> CharacterRelationshipTypes { get; set; } = new();
        public List<LocationFactionRole>       LocationFactionRoles      { get; set; } = new();
        public List<FactionRelationshipType>   FactionRelationshipTypes  { get; set; } = new();
        public List<ItemType>                  ItemTypes                 { get; set; } = new();

        // Entities (IDs preserved so cross-references within the package can be remapped on import)
        public List<Faction>  Factions  { get; set; } = new();
        public List<Npc>      Npcs      { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
        public List<Session>  Sessions  { get; set; } = new();
        public List<Item>     Items     { get; set; } = new();
    }
}
