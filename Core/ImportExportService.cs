using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;

namespace DndBuilder.Core
{
    /// <summary>
    /// What the user has selected in the Import/Export modal.
    /// For export: determines what gets written to the package.
    /// For import: determines which parts of the loaded package get applied.
    /// </summary>
    public class ExportSelection
    {
        // Types
        public bool AllTypes                   { get; set; }
        public bool Species                    { get; set; }
        public bool NpcStatuses                { get; set; }
        public bool NpcRelationshipTypes       { get; set; }
        public bool NpcFactionRoles            { get; set; }
        public bool CharacterRelationshipTypes { get; set; }
        public bool LocationFactionRoles       { get; set; }
        public bool FactionRelationshipTypes   { get; set; }
        public bool ItemTypes                  { get; set; }
        public bool QuestStatuses              { get; set; }

        // Entities
        public bool        AllNpcs      { get; set; }
        public HashSet<int> NpcIds      { get; set; } = new();
        public bool        AllLocations { get; set; }
        public HashSet<int> LocationIds { get; set; } = new();
        public bool        AllFactions  { get; set; }
        public HashSet<int> FactionIds  { get; set; } = new();
        public bool        AllSessions  { get; set; }
        public HashSet<int> SessionIds  { get; set; } = new();
        public bool        AllItems     { get; set; }
        public HashSet<int> ItemIds     { get; set; } = new();
        public bool        AllQuests    { get; set; }
        public HashSet<int> QuestIds    { get; set; } = new();
    }

    public static class ImportExportService
    {
        // ─── Export ──────────────────────────────────────────────────────────

        public static ExportPackage BuildPackage(int campaignId, ExportSelection sel, DatabaseService db)
        {
            var pkg = new ExportPackage();

            // Types
            if (sel.AllTypes || sel.Species)                    pkg.Species                   = db.Species.GetAll(campaignId);
            if (sel.AllTypes || sel.NpcStatuses)                pkg.NpcStatuses               = db.NpcStatuses.GetAll(campaignId);
            if (sel.AllTypes || sel.NpcRelationshipTypes)       pkg.NpcRelationshipTypes      = db.NpcRelationshipTypes.GetAll(campaignId);
            if (sel.AllTypes || sel.NpcFactionRoles)            pkg.NpcFactionRoles           = db.NpcFactionRoles.GetAll(campaignId);
            if (sel.AllTypes || sel.CharacterRelationshipTypes) pkg.CharacterRelationshipTypes = db.CharacterRelationshipTypes.GetAll(campaignId);
            if (sel.AllTypes || sel.LocationFactionRoles)       pkg.LocationFactionRoles      = db.LocationFactionRoles.GetAll(campaignId);
            if (sel.AllTypes || sel.FactionRelationshipTypes)   pkg.FactionRelationshipTypes  = db.FactionRelationshipTypes.GetAll(campaignId);
            if (sel.AllTypes || sel.ItemTypes)                  pkg.ItemTypes                 = db.ItemTypes.GetAll(campaignId);
            if (sel.AllTypes || sel.QuestStatuses)              pkg.QuestStatuses             = db.QuestStatuses.GetAll(campaignId);

            // Entities
            if (sel.AllFactions || sel.FactionIds.Count > 0)
            {
                var all = db.Factions.GetAll(campaignId);
                pkg.Factions = sel.AllFactions ? all : all.Where(f => sel.FactionIds.Contains(f.Id)).ToList();
            }

            if (sel.AllNpcs || sel.NpcIds.Count > 0)
            {
                var all = db.Npcs.GetAll(campaignId);
                pkg.Npcs = sel.AllNpcs ? all : all.Where(n => sel.NpcIds.Contains(n.Id)).ToList();
            }

            if (sel.AllLocations || sel.LocationIds.Count > 0)
            {
                var all = db.Locations.GetAll(campaignId);
                // Export flat — SubLocations are expressed through ParentLocationId
                foreach (var loc in all) loc.SubLocations = new List<Location>();
                pkg.Locations = sel.AllLocations ? all : all.Where(l => sel.LocationIds.Contains(l.Id)).ToList();
            }

            if (sel.AllSessions || sel.SessionIds.Count > 0)
            {
                var all = db.Sessions.GetAll(campaignId);
                pkg.Sessions = sel.AllSessions ? all : all.Where(s => sel.SessionIds.Contains(s.Id)).ToList();
            }

            if (sel.AllItems || sel.ItemIds.Count > 0)
            {
                var all = db.Items.GetAll(campaignId);
                pkg.Items = sel.AllItems ? all : all.Where(i => sel.ItemIds.Contains(i.Id)).ToList();
            }

            if (sel.AllQuests || sel.QuestIds.Count > 0)
            {
                var all = db.Quests.GetAll(campaignId);
                foreach (var q in all) q.History = db.QuestHistory.GetAll(q.Id);
                pkg.Quests = sel.AllQuests ? all : all.Where(q => sel.QuestIds.Contains(q.Id)).ToList();
            }

            return pkg;
        }

        // ─── Import ──────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the selected parts of a package into the target campaign.
        /// All entities are inserted as new records with fresh IDs.
        /// FKs are resolved by name for types; cross-entity FKs use the package's ID remapping.
        /// </summary>
        public static void ApplyPackage(int campaignId, ExportPackage pkg, ExportSelection sel, DatabaseService db)
        {
            // ── Step 1: types ─────────────────────────────────────────────────
            var speciesMap    = ImportTypes(pkg.Species,                   sel.AllTypes || sel.Species,                    campaignId, db.Species.GetAll(campaignId),                   (t, cid) => db.Species.Add(new Models.Species                   { CampaignId = cid, Name = t.Name }));
            var statusMap     = ImportTypes(pkg.NpcStatuses,               sel.AllTypes || sel.NpcStatuses,                campaignId, db.NpcStatuses.GetAll(campaignId),               (t, cid) => db.NpcStatuses.Add(new NpcStatus                   { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var relTypeMap    = ImportTypes(pkg.NpcRelationshipTypes,      sel.AllTypes || sel.NpcRelationshipTypes,       campaignId, db.NpcRelationshipTypes.GetAll(campaignId),      (t, cid) => db.NpcRelationshipTypes.Add(new NpcRelationshipType { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var npcRoleMap    = ImportTypes(pkg.NpcFactionRoles,           sel.AllTypes || sel.NpcFactionRoles,            campaignId, db.NpcFactionRoles.GetAll(campaignId),           (t, cid) => db.NpcFactionRoles.Add(new NpcFactionRole           { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var charRelMap    = ImportTypes(pkg.CharacterRelationshipTypes,sel.AllTypes || sel.CharacterRelationshipTypes, campaignId, db.CharacterRelationshipTypes.GetAll(campaignId),(t, cid) => db.CharacterRelationshipTypes.Add(new CharacterRelationshipType { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var locRoleMap    = ImportTypes(pkg.LocationFactionRoles,      sel.AllTypes || sel.LocationFactionRoles,       campaignId, db.LocationFactionRoles.GetAll(campaignId),      (t, cid) => db.LocationFactionRoles.Add(new LocationFactionRole { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var facRelTypeMap = ImportTypes(pkg.FactionRelationshipTypes,  sel.AllTypes || sel.FactionRelationshipTypes,   campaignId, db.FactionRelationshipTypes.GetAll(campaignId),  (t, cid) => db.FactionRelationshipTypes.Add(new FactionRelationshipType { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var itemTypeMap      = ImportTypes(pkg.ItemTypes,      sel.AllTypes || sel.ItemTypes,      campaignId, db.ItemTypes.GetAll(campaignId),      (t, cid) => db.ItemTypes.Add(new ItemType           { CampaignId = cid, Name = t.Name, Description = t.Description }));
            var questStatusMap   = ImportTypes(pkg.QuestStatuses,  sel.AllTypes || sel.QuestStatuses,  campaignId, db.QuestStatuses.GetAll(campaignId),  (t, cid) => db.QuestStatuses.Add(new QuestStatus   { CampaignId = cid, Name = t.Name, Description = t.Description }));

            // ── Step 2: factions (no FK dependencies on other entities) ───────
            var factionMap = new Dictionary<int, int>();
            var factionsToImport = GetEntities(pkg.Factions, sel.AllFactions, sel.FactionIds);
            foreach (var f in factionsToImport)
            {
                var newId = db.Factions.Add(new Faction
                {
                    CampaignId  = campaignId,
                    Name        = f.Name,
                    Type        = f.Type,
                    Description = f.Description,
                    Goals       = f.Goals,
                    Notes       = f.Notes,
                    Reputation  = f.Reputation,
                });
                factionMap[f.Id] = newId;
            }

            // ── Step 3: locations — two passes for parent-child hierarchy ──────
            var locationMap   = new Dictionary<int, int>();
            var locsToImport  = GetEntities(pkg.Locations, sel.AllLocations, sel.LocationIds);
            foreach (var loc in locsToImport)
            {
                var newId = db.Locations.Add(new Location
                {
                    CampaignId       = campaignId,
                    Name             = loc.Name,
                    Type             = loc.Type,
                    Description      = loc.Description,
                    Notes            = loc.Notes,
                    ParentLocationId = null, // resolved in second pass
                });
                locationMap[loc.Id] = newId;
            }
            // Second pass: fix up parent IDs
            foreach (var loc in locsToImport.Where(l => l.ParentLocationId.HasValue))
            {
                if (!locationMap.TryGetValue(loc.Id, out var newLocId)) continue;
                if (!locationMap.TryGetValue(loc.ParentLocationId.Value, out var newParentId)) continue;
                db.Locations.Edit(new Location
                {
                    Id               = newLocId,
                    CampaignId       = campaignId,
                    Name             = loc.Name,
                    Type             = loc.Type,
                    Description      = loc.Description,
                    Notes            = loc.Notes,
                    ParentLocationId = newParentId,
                });
            }
            // Add location-faction associations
            foreach (var loc in locsToImport)
            {
                if (!locationMap.TryGetValue(loc.Id, out var newLocId)) continue;
                foreach (var lf in loc.Factions)
                {
                    if (!factionMap.TryGetValue(lf.FactionId, out var newFacId)) continue;
                    int? newRoleId = lf.RoleId.HasValue && locRoleMap.TryGetValue(lf.RoleId.Value, out var nr) ? nr : null;
                    db.Locations.AddFaction(newLocId, newFacId, newRoleId);
                }
            }

            // ── Step 4: sessions ──────────────────────────────────────────────
            // Assign numbers sequentially after the current max to avoid the UNIQUE(campaign_id, number) constraint.
            var sessionMap    = new Dictionary<int, int>();
            var sessionsToAdd = GetEntities(pkg.Sessions, sel.AllSessions, sel.SessionIds);
            if (sessionsToAdd.Count > 0)
            {
                var existing   = db.Sessions.GetAll(campaignId);
                int nextNumber = existing.Count > 0 ? existing.Max(s => s.Number) + 1 : 1;
                foreach (var s in sessionsToAdd)
                {
                    var newId = db.Sessions.Add(new Session
                    {
                        CampaignId = campaignId,
                        Number     = nextNumber++,
                        Title      = s.Title,
                        Notes      = s.Notes,
                        PlayedOn   = s.PlayedOn,
                    });
                    sessionMap[s.Id] = newId;
                }
            }

            // ── Step 5: NPCs ──────────────────────────────────────────────────
            var npcMap = new Dictionary<int, int>();
            foreach (var npc in GetEntities(pkg.Npcs, sel.AllNpcs, sel.NpcIds))
            {
                var remappedFactions = npc.Factions
                    .Where(f => factionMap.ContainsKey(f.FactionId))
                    .Select(f => new NpcFaction
                    {
                        FactionId = factionMap[f.FactionId],
                        RoleId    = f.RoleId.HasValue && npcRoleMap.TryGetValue(f.RoleId.Value, out var nr) ? nr : null,
                    })
                    .ToList();

                var newNpc = new Npc
                {
                    CampaignId         = campaignId,
                    Name               = npc.Name,
                    PortraitPath       = npc.PortraitPath,
                    Gender             = npc.Gender,
                    Occupation         = npc.Occupation,
                    Description        = npc.Description,
                    Personality        = npc.Personality,
                    Notes              = npc.Notes,
                    SpeciesId          = Remap(npc.SpeciesId, speciesMap),
                    StatusId           = Remap(npc.StatusId, statusMap),
                    RelationshipTypeId = Remap(npc.RelationshipTypeId, relTypeMap),
                    HomeLocationId     = Remap(npc.HomeLocationId, locationMap),
                    Factions           = remappedFactions,
                    FactionIds         = remappedFactions.ConvertAll(f => f.FactionId),
                };
                npcMap[npc.Id] = db.Npcs.Add(newNpc);
            }

            // ── Step 6: items ─────────────────────────────────────────────────
            foreach (var item in GetEntities(pkg.Items, sel.AllItems, sel.ItemIds))
            {
                db.Items.Add(new Item
                {
                    CampaignId  = campaignId,
                    Name        = item.Name,
                    Description = item.Description,
                    Notes       = item.Notes,
                    IsUnique    = item.IsUnique,
                    TypeId      = Remap(item.TypeId, itemTypeMap),
                });
            }

            // ── Step 7: quests (depends on quest_statuses, locations, npcs) ──
            foreach (var quest in GetEntities(pkg.Quests, sel.AllQuests, sel.QuestIds))
            {
                var newQuestId = db.Quests.Add(new Quest
                {
                    CampaignId   = campaignId,
                    Name         = quest.Name,
                    Description  = quest.Description,
                    Notes        = quest.Notes,
                    Reward       = quest.Reward,
                    StatusId     = Remap(quest.StatusId,     questStatusMap),
                    QuestGiverId = Remap(quest.QuestGiverId, npcMap),
                    LocationId   = Remap(quest.LocationId,   locationMap),
                });
                foreach (var h in quest.History)
                {
                    db.QuestHistory.Add(new QuestHistory
                    {
                        QuestId   = newQuestId,
                        SessionId = Remap(h.SessionId, sessionMap),
                        Note      = h.Note,
                    });
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts type records that don't already exist by name.
        /// Returns old_id → new_id map for FK remapping.
        /// </summary>
        private static Dictionary<int, int> ImportTypes<T>(
            List<T> items,
            bool shouldImport,
            int campaignId,
            List<T> existing,
            System.Func<T, int, int> insert)
            where T : class
        {
            var map = new Dictionary<int, int>();
            if (!shouldImport || items.Count == 0) return map;

            // Use reflection-free approach: T must have Id and Name
            // We access them via dynamic or interface — use interface pattern
            foreach (var item in items)
            {
                var (oldId, name, _) = GetTypeFields(item);
                var match = existing.FirstOrDefault(e => GetTypeFields(e).name == name);
                if (match != null)
                    map[oldId] = GetTypeFields(match).id;
                else
                    map[oldId] = insert(item, campaignId);
            }
            return map;
        }

        private static (int id, string name, string description) GetTypeFields<T>(T item)
        {
            // All seeded type models have Id, Name, Description
            var type = item.GetType();
            var id   = (int)type.GetProperty("Id")!.GetValue(item)!;
            var name = (string)type.GetProperty("Name")!.GetValue(item)!;
            var desc = (string)(type.GetProperty("Description")?.GetValue(item) ?? "");
            return (id, name, desc);
        }

        private static List<T> GetEntities<T>(List<T> all, bool selectAll, HashSet<int> ids)
            where T : class
        {
            if (!selectAll && ids.Count == 0) return new List<T>();
            if (selectAll) return all;
            var idProp = typeof(T).GetProperty("Id")!;
            return all.Where(e => ids.Contains((int)idProp.GetValue(e)!)).ToList();
        }

        private static int? Remap(int? oldId, Dictionary<int, int> map)
        {
            if (!oldId.HasValue) return null;
            return map.TryGetValue(oldId.Value, out var newId) ? newId : null;
        }
    }
}
