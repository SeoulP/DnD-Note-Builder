using System.Collections.Generic;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;

namespace DndBuilder.Core
{
    /// <summary>
    /// Pure calculation service — no DB writes.
    /// Returns expected skill slot counts per source for a given PlayerCharacter.
    /// </summary>
    public class SkillExpectationService
    {
        private readonly ClassRepository         _classes;
        private readonly AbilityRepository       _abilities;
        private readonly DnD5eBackgroundRepository _backgrounds;

        public SkillExpectationService(
            ClassRepository classes,
            AbilityRepository abilities,
            DnD5eBackgroundRepository backgrounds)
        {
            _classes     = classes;
            _abilities   = abilities;
            _backgrounds = backgrounds;
        }

        public List<SkillExpectation> GetExpectations(PlayerCharacter pc)
        {
            var result = new List<SkillExpectation>();

            // ── Class ─────────────────────────────────────────────────────────
            // Base skill grant stored directly on the Class record.
            if (pc.ClassId.HasValue)
            {
                var cls = _classes.Get(pc.ClassId.Value);
                if (cls != null && cls.SkillChoicesCount > 0)
                    result.Add(new SkillExpectation
                    {
                        Source        = "class",
                        SourceId      = cls.Id,
                        SourceName    = cls.Name,
                        ExpectedCount = cls.SkillChoicesCount,
                    });
            }

            // ── Background ────────────────────────────────────────────────────
            if (pc.BackgroundId.HasValue)
            {
                var bg = _backgrounds.Get(pc.BackgroundId.Value);
                if (bg != null && bg.SkillCount > 0)
                    result.Add(new SkillExpectation
                    {
                        Source        = "background",
                        SourceId      = bg.Id,
                        SourceName    = bg.Name,
                        ExpectedCount = bg.SkillCount,
                    });
            }

            // ── Feats / Other abilities ───────────────────────────────────────
            // Any owned ability (class levels, subclass, species, subspecies)
            // with ChoicePoolType == "skill" contributes a feat-style skill grant.
            // The base class grant is tracked via SkillChoicesCount above, so
            // these are genuinely additional grants (feats, species traits, etc.).
            foreach (var abilityId in GetAllOwnedAbilityIds(pc))
            {
                var ability = _abilities.Get(abilityId);
                if (ability?.ChoicePoolType != "skill") continue;

                int count = _abilities.ResolveChoiceCount(ability, pc.Level, pc);
                if (count <= 0) continue;

                result.Add(new SkillExpectation
                {
                    Source        = "feat",
                    SourceId      = ability.Id,
                    SourceName    = ability.Name,
                    ExpectedCount = count,
                });
            }

            return result;
        }

        private HashSet<int> GetAllOwnedAbilityIds(PlayerCharacter pc)
        {
            var ids = new HashSet<int>();

            if (pc.ClassId.HasValue)
                foreach (var level in _classes.GetLevelsForClass(pc.ClassId.Value))
                {
                    if (level.Level > pc.Level) break;
                    foreach (var id in _abilities.GetAbilityIdsForLevel(level.Id))
                        ids.Add(id);
                }

            if (pc.SubclassId.HasValue)
                foreach (var id in _abilities.GetAbilityIdsForSubclass(pc.SubclassId.Value, pc.Level))
                    ids.Add(id);

            if (pc.SpeciesId.HasValue)
                foreach (var id in _abilities.GetAbilityIdsForSpecies(pc.SpeciesId.Value))
                    ids.Add(id);

            if (pc.SubspeciesId.HasValue)
                foreach (var id in _abilities.GetAbilityIdsForSubspecies(pc.SubspeciesId.Value))
                    ids.Add(id);

            return ids;
        }
    }
}
