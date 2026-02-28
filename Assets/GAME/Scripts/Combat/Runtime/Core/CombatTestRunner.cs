using UnityEngine;
using Game.Combat.Adapters;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.Data;

namespace Game.Combat.Core
{
    public sealed class CombatTestRunner : MonoBehaviour
    {
        [Header("Assign 2 skills SO for MVP test")]
        public SkillDefinitionSO basicAttackSO; // cost 0, tag Attack
        public SkillDefinitionSO fireSkillSO;   // cost 1~, keywords Fire

        private CombatStateMachine _sm;
        private CombatSession _session;
        private SkillBook _book;

        private void Start()
        {
            var inspiration = new InspirationPool(max: 10, startValue: 3);
            var env = new CombatEnvironment();

            // 선공: PlayerFirstHit => Allies initiative
            _session = new CombatSession(StartReason.PlayerFirstHit, Side.Allies, inspiration, env);

            _book = new SkillBook();
            var basic = new SoSkill(basicAttackSO);
            var fire = new SoSkill(fireSkillSO);
            _book.Register(basic);
            _book.Register(fire);

            var a1 = new DummyCombatant(1, Side.Allies, hp: 10, weakness: KeywordMask.None, staggerMax: 6);
            a1.AddSkill(basic);
            a1.AddSkill(fire);

            var e1 = new DummyCombatant(100, Side.Enemies, hp: 12, weakness: KeywordMask.Fire, staggerMax: 8);
            e1.AddSkill(basic);

            _session.Allies.Add(a1);
            _session.Enemies.Add(e1);

            _sm = new CombatStateMachine(_session, new CombatTimeline(), _book);

            // EnterCombat → Planning까지 진행
            _sm.Tick();

            // Planning: 아군 2슬롯 예약
            // 슬롯1: 불 스킬로 적 공격 (약점이면 그로기 보너스)
            // 슬롯2: 기본 공격
            _session.CurrentTurn.SetPlan(
                a1.Id,
                new ActionPlan(
                    new PlannedAction(fire.Id, fire.Tag, fire.Targeting, e1.Id, fire.Speed, fire.ConsumesTurn),
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, e1.Id, basic.Speed, basic.ConsumesTurn)
                )
            );

            // 적 2슬롯 예약(기본 공격 2번)
            _session.CurrentTurn.SetPlan(
                e1.Id,
                new ActionPlan(
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, a1.Id, basic.Speed, basic.ConsumesTurn),
                    new PlannedAction(basic.Id, basic.Tag, basic.Targeting, a1.Id, basic.Speed, basic.ConsumesTurn)
                )
            );

            _sm.ConfirmPlanning();
        }

        private void Update()
        {
            if (_sm == null) return;

            // Resolution → EndTurn → Planning(+new turn)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _sm.Tick(); // Resolution
                DumpEvents();

                _sm.Tick(); // EndTurn -> BeginNewTurn -> Planning
                Debug.Log($"Turn {_session.TurnIndex} begins. Inspiration={_session.Inspiration.Current}/{_session.Inspiration.Max}");
            }
        }

        private void DumpEvents()
        {
            for (int i = 0; i < _session.CurrentTurn.Events.Count; i++)
                Debug.Log(_session.CurrentTurn.Events[i].Message);

            _session.CurrentTurn.Events.Clear();
        }
    }
}
