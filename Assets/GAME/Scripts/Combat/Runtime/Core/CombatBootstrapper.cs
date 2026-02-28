using Game.Combat.Adapters;
using Game.Combat.Environment;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// 필드에서 전투 시작 요청을 받으면, CombatSession + StateMachine을 구성해 반환.
    /// </summary>
    public static class CombatBootstrapper
    {
        public static (CombatSession session, CombatStateMachine stateMachine) StartCombat(
            CombatStartRequest req,
            SkillBook skillBook,
            ICombatantFactory combatantFactory = null)
        {
            var inspiration = new InspirationPool(req.InspirationMax, req.InspirationStart);
            var env = new CombatEnvironment();

            var session = new CombatSession(req.Reason, req.InitiativeSide, inspiration, env);

            // 전투원 생성(필드 연동 전에는 DummyFactory로 대체 가능)
            combatantFactory ??= new DummyCombatantFactory(skillBook);
            combatantFactory.PopulateCombatants(session, req);

            var timeline = new CombatTimeline();
            var sm = new CombatStateMachine(session, timeline, skillBook);

            // EnterCombat 1틱 해서 Turn 생성(Events 기록 가능하게)
            sm.Tick();

            // OpeningEffect 적용(특수 선제 등)
            OpeningEffectApplier.ApplyIfAny(session, req.OpeningEffectOrNull);

            return (session, sm);
        }
    }
}
