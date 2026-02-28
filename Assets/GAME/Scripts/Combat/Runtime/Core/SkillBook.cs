using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class SkillBook
    {
        private readonly Dictionary<int, ISkill> _map = new();

        public void Register(ISkill skill)
        {
            _map[skill.Id.Value] = skill;
        }

        public ISkill Get(SkillId id)
        {
            _map.TryGetValue(id.Value, out var skill);
            return skill;
        }
    }
}
