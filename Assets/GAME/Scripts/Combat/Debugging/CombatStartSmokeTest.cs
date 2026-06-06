using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Adapters;
using Game.Combat.Actions;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine.InputSystem;

namespace Game.Combat.Core
{


    public sealed class CombatStartSmokeTest : MonoBehaviour
    {
        [Header("Skills (same as CombatTestRunner)")]
        public SkillDefinitionSO basicAttackSO;
        public SkillDefinitionSO fireSkillSO;

        [Header("Opening Effect (optional)")]
        public OpeningEffectSO openingEffect;

        private CombatSession _session;
        private CombatStateMachine _sm;

        [SerializeField] private InputActionReference debugStep; // Space에 연결할 액션

        private void Start()
        {
            // 1) 스킬북 구성
            var book = new SkillBook();
            if (basicAttackSO != null) book.Register(new SoSkill(basicAttackSO));
            if (fireSkillSO != null) book.Register(new SoSkill(fireSkillSO));

            // 2) 필드에서 넘어온 것처럼 StartRequest 구성 (더미 오브젝트 리스트)
            var req = new CombatStartRequest(
                reason: StartReason.SpecialSkill,      // 특수 선제 테스트
                initiativeSide: Side.Allies,           // 선공=아군
                inspirationMax: 10,
                inspirationStart: 3,
                openingEffectOrNull: openingEffect
            );

            // 더미로 3 아군, 2 적 있다고 가정
            req.AllyFieldObjects.Add(new GameObject("DummyAllyA"));
            req.AllyFieldObjects.Add(new GameObject("DummyAllyB"));
            req.AllyFieldObjects.Add(new GameObject("DummyAllyC"));

            req.EnemyFieldObjects.Add(new GameObject("DummyEnemy1"));
            req.EnemyFieldObjects.Add(new GameObject("DummyEnemy2"));

            // 3) 전투 시작
            (_session, _sm) = CombatBootstrapper.StartCombat(req, book);

            Debug.Log($"[CombatStart] Reason={_session.StartReason}, Initiative={_session.InitiativeSide}");
            Debug.Log($"[CombatStart] Turn={_session.TurnIndex}, Inspiration={_session.Inspiration.Current}/{_session.Inspiration.Max}");
            Debug.Log($"[CombatStart] Allies={_session.Allies.Count}, Enemies={_session.Enemies.Count}");

            DumpEvents();
        }

        private void Update()
        {
            if (_sm == null) return;

            if (debugStep != null && debugStep.action != null && debugStep.action.WasPressedThisFrame())
            {
                _sm.Tick();
                DumpEvents();
            }
        }


        private void DumpEvents()
        {
            if (_session == null) return;
            for (int i = 0; i < _session.CurrentTurn.Events.Count; i++)
                Debug.Log(_session.CurrentTurn.Events[i].Message);

            _session.CurrentTurn.Events.Clear();
        }
    }
}
