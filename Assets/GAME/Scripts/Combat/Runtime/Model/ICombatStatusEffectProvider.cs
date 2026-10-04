using System.Collections.Generic;
using Game.Combat.Data;

namespace Game.Combat.Model
{
    public interface ICombatStatusEffectProvider
    {
        IReadOnlyList<CombatStatusDefinitionSO> AppliedStatusEffects { get; }
    }
}
