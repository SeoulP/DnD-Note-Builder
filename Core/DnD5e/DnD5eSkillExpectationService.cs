using System.Collections.Generic;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;

namespace DndBuilder.Core
{
    /// <summary>
    /// Pure calculation service — no DB writes.
    /// Returns expected skill slot counts per source for a given DnD5ePlayerCharacter.
    /// </summary>
    public class DnD5eSkillExpectationService
    {
        private readonly DnD5eClassRepository         _classes;
        private readonly DnD5eAbilityRepository       _abilities;
        private readonly DnD5eBackgroundRepository _backgrounds;

        public DnD5eSkillExpectationService(
            DnD5eClassRepository classes,
            DnD5eAbilityRepository abilities,
            DnD5eBackgroundRepository backgrounds)
        {
            _classes     = classes;
            _abilities   = abilities;
            _backgrounds = backgrounds;
        }

        public List<DnD5eSkillExpectation> GetExpectations(DnD5ePlayerCharacter pc)
        {
            var result = new List<DnD5eSkillExpectation>();

            // ── DnD5eClass ─────────────────────────────────────────────────────────
            // Base skill grant stored directly on the DnD5eClass record.
            if (pc.ClassId.HasValue)
            {
                var cls = _classes.Get(pc.ClassId.Value);
                if (cls != null && cls.SkillChoicesCount > 0)
                    result.Add(new DnD5eSkillExpectation
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
                    result.Add(new DnD5eSkillExpectation
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

                result.Add(new DnD5eSkillExpectation
                {
                    Source        = "feat",
                    SourceId      = ability.Id,
                    SourceName    = ability.Name,
                    ExpectedCount = count,
                });
            }

            return result;
        }

        private HashSet<int> GetAllOwnedAbilityIds(DnD5ePlayerCharacter pc)
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
