using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatTestRunner : MonoBehaviour
    {
        [Header("Assign 2 skills SO for MVP test")]
        public SkillDefinitionSO basicAttackSO;
        public SkillDefinitionSO fireSkillSO;

        private CombatStateMachine _sm;
        private CombatSession _session;
        private SkillBook _book;

        private void Start()
        {
            InspirationPool inspiration = new InspirationPool(max: 10, startValue: 3);
            CombatEnvironment environment = new CombatEnvironment();
            _session = new CombatSession(StartReason.PlayerFirstHit, Side.Allies, inspiration, environment);

            _book = new SkillBook();
            SoSkill basic = new SoSkill(basicAttackSO);
            SoSkill fire = new SoSkill(fireSkillSO);
            _book.Register(basic);
            _book.Register(fire);

            DummyCombatant ally = new DummyCombatant(1, Side.Allies, hp: 10, weakness: KeywordMask.None, staggerMax: 6);
            ally.AddSkill(basic);
            ally.AddSkill(fire);

            DummyCombatant enemy = new DummyCombatant(100, Side.Enemies, hp: 12, weakness: KeywordMask.Fire, staggerMax: 8);
            enemy.AddSkill(basic);

            _session.Allies.Add(ally);
            _session.Enemies.Add(enemy);
            _sm = new CombatStateMachine(_session, new CombatTimeline(), _book);
            _sm.Tick();

            _session.CurrentTurn.SetPlan(
                ally.Id,
                new ActionPlan(
                    new PlannedAction(fire.Id, fire.Tag, fire.Targeting, enemy.Id, fire.Speed, fire.ConsumesTurn),
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, enemy.Id, basic.Speed, basic.ConsumesTurn)));

            _session.CurrentTurn.SetPlan(
                enemy.Id,
                new ActionPlan(
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, ally.Id, basic.Speed, basic.ConsumesTurn),
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, ally.Id, basic.Speed, basic.ConsumesTurn)));

            _sm.ConfirmPlanning();
        }

        private void Update()
        {
            if (_sm == null || !UnityEngine.Input.GetKeyDown(KeyCode.Space))
                return;

            _sm.Tick();
            DumpEvents();
            _sm.Tick();
            Debug.Log($"Turn {_session.TurnIndex} begins. Inspiration={_session.Inspiration.Current}/{_session.Inspiration.Max}");
        }

        private void DumpEvents()
        {
            for (int i = 0; i < _session.CurrentTurn.Events.Count; i++)
                Debug.Log(_session.CurrentTurn.Events[i].Message);

            _session.CurrentTurn.ClearDebugEvents();
        }
    }
}
