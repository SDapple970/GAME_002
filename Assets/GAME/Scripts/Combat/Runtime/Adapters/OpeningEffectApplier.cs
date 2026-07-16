using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    public static class OpeningEffectApplier
    {
        public static void ApplyIfAny(CombatSession session, OpeningEffectSO opening)
        {
            if (opening == null)
                return;

            if (opening.inspirationDelta > 0)
                session.Inspiration.Gain(opening.inspirationDelta);
            else if (opening.inspirationDelta < 0)
                session.Inspiration.TrySpend(-opening.inspirationDelta);

            if (opening.addEnemyStagger != 0)
            {
                foreach (ICombatant enemy in session.Enemies)
                    StaggerSystem.AddStagger(enemy, opening.addEnemyStagger);
            }

            if (opening.addAllyStagger != 0)
            {
                foreach (ICombatant ally in session.Allies)
                    StaggerSystem.AddStagger(ally, opening.addAllyStagger);
            }

            session.CurrentTurn.AddResolvedEvent(new ResolvedEvent($"OpeningEffect applied: {opening.name}"));
        }
    }
}
