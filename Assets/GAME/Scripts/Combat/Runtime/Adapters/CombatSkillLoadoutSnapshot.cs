using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    /// <summary>Immutable resolved combat skills carried across the integration-to-session boundary.</summary>
    public sealed class CombatSkillLoadoutSnapshot
    {
        private readonly ISkill[] _skills;
        public bool IncludeCompatibilityLoadout { get; }

        public IReadOnlyList<ISkill> Skills => Array.AsReadOnly(_skills);

        public CombatSkillLoadoutSnapshot(IEnumerable<ISkill> skills)
            : this(skills, false)
        {
        }

        public CombatSkillLoadoutSnapshot(IEnumerable<ISkill> skills, bool includeCompatibilityLoadout)
        {
            IncludeCompatibilityLoadout = includeCompatibilityLoadout;
            List<ISkill> copy = new();
            if (skills != null)
            {
                foreach (ISkill skill in skills)
                    if (skill != null) copy.Add(skill);
            }

            _skills = copy.ToArray();
        }
    }
}
