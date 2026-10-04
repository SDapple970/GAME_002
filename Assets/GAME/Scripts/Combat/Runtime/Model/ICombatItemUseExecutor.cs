using System.Collections.Generic;

namespace Game.Combat.Model
{
    public interface ICombatItemUseExecutor
    {
        IReadOnlyList<CombatItemOption> GetUsableItems(CombatSession session, ICombatant user);
        IReadOnlyList<ICombatant> GetUsableTargets(
            CombatSession session,
            ICombatant user,
            CombatItemOption item);
        CombatItemUseResult TryUse(CombatSession session, CombatItemUseRequest request);
    }
}
