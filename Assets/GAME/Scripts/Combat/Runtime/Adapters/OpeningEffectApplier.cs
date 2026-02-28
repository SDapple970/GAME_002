using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Adapters
{
    public static class OpeningEffectApplier
    {
        public static void ApplyIfAny(CombatSession session, OpeningEffectSO opening)
        {
            if (opening == null) return;

            // 영감 변화
            if (opening.inspirationDelta > 0) session.Inspiration.Gain(opening.inspirationDelta);
            else if (opening.inspirationDelta < 0) session.Inspiration.TrySpend(-opening.inspirationDelta);

            // 적/아군 그로기 초기 보정
            if (opening.addEnemyStagger != 0)
            {
                foreach (var e in session.Enemies)
                    StaggerSystem.AddStagger(e, opening.addEnemyStagger);
            }

            if (opening.addAllyStagger != 0)
            {
                foreach (var a in session.Allies)
                    StaggerSystem.AddStagger(a, opening.addAllyStagger);
            }

            session.CurrentTurn.Events.Add(new ResolvedEvent($"OpeningEffect applied: {opening.name}"));
        }
    }
}
