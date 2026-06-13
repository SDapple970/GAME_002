// GAME/Scripts/Battle/FieldEnemy.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Game.Core;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.DemoMission.Runtime;

namespace Game.Battle
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldEnemy : MonoBehaviour, Game.Common.IDamageable
    {
        public static event Action<BattleTransitionRequest> OnBattleRequested;

        [Header("Combat Entry")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string battleSceneName = "Battle";
        [SerializeField] private OpeningEffectSO openingEffectOrNull;

        [Header("Encounter Settings")]
        [SerializeField] private StartReason touchStartReason = StartReason.PlayerGotHit;
        [SerializeField] private StartReason hitStartReason = StartReason.PlayerFirstHit;

        [Header("AI Settings")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private float aggroRange = 5f;
        [SerializeField] private float moveSpeed = 2.5f;

        [Header("Demo Mission Optional")]
        [FormerlySerializedAs("countAsDemoMissionEnemy")]
        [SerializeField] private bool countForDemoMission;
        [SerializeField] private DemoMissionRuntime demoMissionRuntime;

        private bool _isEncounterTriggered;
        private bool _demoMissionDefeatRegistered;

        private void Awake()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                    playerTarget = player.transform;
            }
        }

        private void OnEnable()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (combatEntryPoint != null)
                combatEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDisable()
        {
            TryRegisterDemoMissionDefeatFromDisable();

            if (combatEntryPoint != null)
                combatEntryPoint.OnCombatEnded -= HandleCombatEnded;
        }

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Update()
        {
            if (_isEncounterTriggered || playerTarget == null)
                return;

            float distance = Vector2.Distance(transform.position, playerTarget.position);
            if (distance <= aggroRange)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerTarget.position,
                    moveSpeed * Time.deltaTime
                );
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isEncounterTriggered)
                return;

            if (!other.CompareTag(playerTag))
                return;

            StartCombat(other.gameObject, Side.Enemies, touchStartReason);
        }

        public void TakeDamage(int amount)
        {
            if (_isEncounterTriggered || !CanStartFieldEncounter())
                return;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
                return;

            StartCombat(player, Side.Allies, hitStartReason);
        }

        private void StartCombat(GameObject playerObject, Side initiativeSide, StartReason reason)
        {
            if (_isEncounterTriggered)
                return;

            if (!CanStartFieldEncounter())
                return;

            if (combatEntryPoint == null || playerObject == null)
            {
                Debug.LogError("[FieldEnemy] CombatEntryPoint or Player is missing.");
                return;
            }

            if (combatEntryPoint.ActiveStateMachine != null)
                return;

            _isEncounterTriggered = true;

            List<GameObject> allies = new List<GameObject>(1) { playerObject };
            List<GameObject> enemies = new List<GameObject>(1) { gameObject };

            bool started = combatEntryPoint.StartCombatFromField(
                allyFieldObjects: allies,
                enemyFieldObjects: enemies,
                reason: reason,
                initiativeSide: initiativeSide,
                openingEffectOrNull: openingEffectOrNull
            );

            if (started)
            {
                Debug.Log($"[FieldEnemy] Combat started. reason={reason}, initiative={initiativeSide}");
                OnBattleRequested?.Invoke(new BattleTransitionRequest(transform.position, battleSceneName, ToEncounterAdvantage(initiativeSide)));
            }
            else
            {
                _isEncounterTriggered = false;
            }
        }

        public void RegisterDemoMissionDefeat()
        {
            if (_demoMissionDefeatRegistered)
                return;

            if (demoMissionRuntime == null)
                demoMissionRuntime = DemoMissionRuntime.GetOrCreate();

            _demoMissionDefeatRegistered = true;
            demoMissionRuntime.RegisterEnemyDefeated();
        }

        private static bool CanStartFieldEncounter()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private static EncounterAdvantage ToEncounterAdvantage(Side initiativeSide)
        {
            return initiativeSide == Side.Allies ? EncounterAdvantage.PlayerFirst : EncounterAdvantage.EnemyFirst;
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (!countForDemoMission || _demoMissionDefeatRegistered)
                return;

            if (!_isEncounterTriggered || result == null || !result.IsWin)
                return;

            RegisterDemoMissionDefeat();
        }

        private void TryRegisterDemoMissionDefeatFromDisable()
        {
            if (!countForDemoMission || _demoMissionDefeatRegistered || !_isEncounterTriggered)
                return;

            HpAccessor hpAccessor = HpAccessor.TryCreate(gameObject);
            if (hpAccessor == null || !hpAccessor.IsValid || hpAccessor.GetHp() > 0)
                return;

            RegisterDemoMissionDefeat();
        }
    }
}
