using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class SkillBook
    {
        private readonly Dictionary<int, ISkill> _map = new();

        public void Register(ISkill skill)
        {
            if (skill == null)
                throw new ArgumentNullException(nameof(skill));

            int skillId = skill.Id.Value;
            if (_map.TryGetValue(skillId, out ISkill existing))
            {
                throw new InvalidOperationException(
                    $"Skill ID {skillId} is already registered by '{existing.Name}'. " +
                    $"Rejected duplicate '{skill.Name}'.");
            }

            _map.Add(skillId, skill);
        }

        public ISkill Get(SkillId id)
        {
            _map.TryGetValue(id.Value, out var skill);
            return skill;
        }
    }
}
