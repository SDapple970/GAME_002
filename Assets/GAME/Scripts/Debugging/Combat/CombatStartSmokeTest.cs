using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine;
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

        [SerializeField] private InputActionReference debugStep;

        private CombatSession _session;
        private CombatStateMachine _sm;

        private void Start()
        {
            SkillBook book = new SkillBook();
            if (basicAttackSO != null)
                book.Register(new SoSkill(basicAttackSO));
            if (fireSkillSO != null)
                book.Register(new SoSkill(fireSkillSO));

            CombatStartRequest request = new CombatStartRequest(
                StartReason.SpecialSkill,
                Side.Allies,
                10,
                3,
                openingEffect);

            request.AllyFieldObjects.Add(new GameObject("DummyAllyA"));
            request.AllyFieldObjects.Add(new GameObject("DummyAllyB"));
            request.AllyFieldObjects.Add(new GameObject("DummyAllyC"));
            request.EnemyFieldObjects.Add(new GameObject("DummyEnemy1"));
            request.EnemyFieldObjects.Add(new GameObject("DummyEnemy2"));

            (_session, _sm) = CombatBootstrapper.StartCombat(request, book);

            Debug.Log($"[CombatStart] Reason={_session.StartReason}, Initiative={_session.InitiativeSide}");
            Debug.Log($"[CombatStart] Turn={_session.TurnIndex}, Inspiration={_session.Inspiration.Current}/{_session.Inspiration.Max}");
            Debug.Log($"[CombatStart] Allies={_session.Allies.Count}, Enemies={_session.Enemies.Count}");
            DumpEvents();
        }

        private void Update()
        {
            if (_sm == null || debugStep?.action == null || !debugStep.action.WasPressedThisFrame())
                return;

            _sm.Tick();
            DumpEvents();
        }

        private void DumpEvents()
        {
            if (_session == null)
                return;

            for (int i = 0; i < _session.CurrentTurn.Events.Count; i++)
                Debug.Log(_session.CurrentTurn.Events[i].Message);

            _session.CurrentTurn.ClearDebugEvents();
        }
    }
}
