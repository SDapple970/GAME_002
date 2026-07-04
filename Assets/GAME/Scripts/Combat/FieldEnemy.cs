// GAME/Scripts/Battle/FieldEnemy.cs
using System;
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
        [Obsolete("Legacy only. Production field enemies now start combat through CombatStartRequest and CombatEntryPoint.")]
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
        private bool _duplicateEncounterWarned;

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
            {
                WarnDuplicateEncounterBlocked();
                return;
            }

            if (!other.CompareTag(playerTag))
                return;

            StartCombat(other.gameObject, Side.Enemies, touchStartReason);
        }

        public void TakeDamage(int amount)
        {
            if (_isEncounterTriggered)
            {
                WarnDuplicateEncounterBlocked();
                return;
            }

            if (!CanStartFieldEncounter())
                return;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
                return;

            StartCombat(player, Side.Allies, hitStartReason);
        }

        private void StartCombat(GameObject playerObject, Side initiativeSide, StartReason reason)
        {
            if (_isEncounterTriggered)
            {
                WarnDuplicateEncounterBlocked();
                return;
            }

            if (!CanStartFieldEncounter())
                return;

            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (combatEntryPoint == null)
            {
                Debug.LogError("[FieldEnemy] CombatEntryPoint is missing.", this);
                return;
            }

            if (combatEntryPoint.ActiveSession != null || combatEntryPoint.ActiveStateMachine != null)
            {
                WarnDuplicateEncounterBlocked();
                return;
            }

            if (!TryCreateEncounterRequest(playerObject, initiativeSide, reason, out CombatStartRequest request))
                return;

            _isEncounterTriggered = true;

            bool started = combatEntryPoint.StartCombat(request);

            if (started)
            {
                Debug.Log($"[FieldEnemy] Combat started. reason={reason}, initiative={initiativeSide}");
            }
            else
            {
                _isEncounterTriggered = false;
            }
        }

        public bool CountsForDemoMission => countForDemoMission;

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

        private bool TryCreateEncounterRequest(GameObject playerObject, Side initiativeSide, StartReason reason, out CombatStartRequest request)
        {
            request = null;

            if (playerObject == null)
            {
                Debug.LogWarning("[FieldEnemy] Encounter request missing player object.", this);
                return false;
            }

            request = new CombatStartRequest(
                reason,
                initiativeSide,
                0,
                -1,
                openingEffectOrNull
            );

            request.AllyFieldObjects.Add(playerObject);
            request.EnemyFieldObjects.Add(gameObject);
            return true;
        }

        private void WarnDuplicateEncounterBlocked()
        {
            if (_duplicateEncounterWarned)
                return;

            _duplicateEncounterWarned = true;
            Debug.LogWarning("[FieldEnemy] Duplicate encounter start blocked.", this);
        }

        [Obsolete("Legacy only. Production field enemies no longer publish BattleTransitionRequest.")]
        public BattleTransitionRequest CreateLegacyBattleTransitionRequest(Side initiativeSide)
        {
            return new BattleTransitionRequest(transform.position, battleSceneName, ToEncounterAdvantage(initiativeSide));
        }

        private static EncounterAdvantage ToEncounterAdvantage(Side initiativeSide)
        {
            return initiativeSide == Side.Allies ? EncounterAdvantage.PlayerFirst : EncounterAdvantage.EnemyFirst;
        }
    }
}
