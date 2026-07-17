// Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.Core;
using Game.Player;

namespace Game.Combat.Integration
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CombatEncounterTrigger2D : MonoBehaviour, ICombatEncounterRuntimeOwner
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Enemy")]
        [SerializeField] private GameObject enemyObject;
        [SerializeField] private CombatEncounterGroup encounterGroup;

        [Header("Opening / Initiative")]
        [SerializeField] private OpeningEffectSO openingEffectOrNull;
        [SerializeField] private StartReason startReason = StartReason.PlayerFirstHit;
        [SerializeField] private Side initiativeSide = Side.Allies;

        [Header("Filter")]
        [SerializeField] private string playerTag = "Player";

        [Header("Debug")]
        [SerializeField] private bool debugLog;

        private Collider2D _trigger;
        private bool _armed = true;
        private bool _requestInProgress;
        private int _lastRequestFrame = -1;
        private readonly HashSet<int> _playerColliderIds = new();
        private UnityEngine.Object _reservationOwner;
        private EncounterRuntimeLifecycle _lifecycle;
        private string _activeCompletionId;
        private string _processedCompletionId;
        private bool _explorationObserved;

        internal ICombatEncounterRuntimeOwner RuntimeOwner => encounterGroup != null ? encounterGroup : this;
        internal EncounterRuntimeLifecycle Lifecycle => encounterGroup != null ? encounterGroup.Lifecycle : _lifecycle;
        internal string ActiveCompletionId => encounterGroup != null ? encounterGroup.ActiveCompletionId : _activeCompletionId;
        internal bool HasPlayerPresence => encounterGroup != null ? encounterGroup.HasPlayerPresence : _playerColliderIds.Count > 0;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (enemyObject == null)
                enemyObject = gameObject;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            LogDebug($"OnTriggerEnter2D other='{GetColliderName(other)}', tag='{GetColliderTag(other)}'.");

            GameObject playerRoot = ResolvePlayerRoot(other);
            if (playerRoot == null)
            {
                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because a player root could not be resolved.");
                Debug.LogWarning("[CombatEncounterTrigger2D] Player root could not be resolved from the trigger collider. Check PlayerInputController, CombatHpComponent, or Player tag placement.", this);
                return;
            }

            RuntimeOwner.RegisterPlayerCollider(other);

            if (RuntimeOwner.Lifecycle != EncounterRuntimeLifecycle.Idle ||
                _requestInProgress ||
                _lastRequestFrame == Time.frameCount)
            {
                _armed = false;
                LogDebug($"Ignored trigger from {GetColliderName(other)} because lifecycle is {RuntimeOwner.Lifecycle} or a request is already processing.");
                return;
            }

            if (!RuntimeOwner.TryReserve(this))
            {
                _armed = false;
                LogDebug($"Encounter reservation rejected in lifecycle {RuntimeOwner.Lifecycle}.");
                return;
            }

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (entryPoint == null)
            {
                RuntimeOwner.ReleaseReservation(this);
                Debug.LogError("[CombatEncounterTrigger2D] EntryPoint is missing.");
                return;
            }

            if (enemyObject == null)
                enemyObject = gameObject;

            List<GameObject> allies = new List<GameObject>(1)
            {
                playerRoot
            };

            List<GameObject> enemies;

            if (encounterGroup != null)
            {
                enemies = encounterGroup.GetActiveEnemies();
            }
            else
            {
                enemies = new List<GameObject>(1) { enemyObject };
            }

            enemies.RemoveAll(go => go == null || !go.activeInHierarchy);

            if (enemies.Count == 0)
            {
                RuntimeOwner.ReleaseReservation(this);
                Debug.LogWarning("[CombatEncounterTrigger2D] No active enemies found.");
                return;
            }

            CombatStartRequest request = CreateEncounterRequest(allies, enemies);
            bool started = false;
            _requestInProgress = true;
            _lastRequestFrame = Time.frameCount;
            try
            {
                started = entryPoint.StartCombat(request);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CombatEncounterTrigger2D] Combat start threw and the encounter reservation was released. {exception}", this);
            }
            finally
            {
                _requestInProgress = false;
                if (!started)
                    RuntimeOwner.ReleaseReservation(this);
            }

            if (started)
            {
                _armed = false;
                RuntimeOwner.CommitReservation(entryPoint.ActiveSession != null
                    ? entryPoint.ActiveSession.CompletionId
                    : RuntimeOwner.ActiveCompletionId);
                Debug.Log(
                    $"[CombatEncounterTrigger2D] Combat started. " +
                    $"Allies={allies.Count}, Enemies={enemies.Count}, Reason={startReason}, Initiative={initiativeSide}"
                );
            }
            else
            {
                _armed = true;
                LogDebug(
                    "StartCombat returned false; encounter remains armed. " +
                    $"GameState={GetCurrentGameStateText()}, " +
                    $"ActiveSessionNull={entryPoint.ActiveSession == null}, " +
                    $"ActiveStateMachineNull={entryPoint.ActiveStateMachine == null}, " +
                    $"Allies={allies.Count}, Enemies={enemies.Count}"
                );
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (ResolvePlayerRoot(other) == null)
                return;

            RuntimeOwner.UnregisterPlayerCollider(other);
            _armed = RuntimeOwner.Lifecycle == EncounterRuntimeLifecycle.Idle;
        }

        internal bool TryReserve(UnityEngine.Object requester)
        {
            if (encounterGroup != null)
                return encounterGroup.TryReserve(requester);

            if (requester == null || _lifecycle != EncounterRuntimeLifecycle.Idle)
                return false;

            _reservationOwner = requester;
            _lifecycle = EncounterRuntimeLifecycle.StartReserved;
            _explorationObserved = false;
            return true;
        }

        internal void CommitReservation(string completionId)
        {
            if (encounterGroup != null)
            {
                encounterGroup.CommitReservation(completionId);
                return;
            }

            if ((_lifecycle != EncounterRuntimeLifecycle.StartReserved &&
                 _lifecycle != EncounterRuntimeLifecycle.ActiveCombat) ||
                string.IsNullOrWhiteSpace(completionId))
            {
                return;
            }

            _reservationOwner = null;
            _activeCompletionId = completionId;
            _processedCompletionId = null;
            _lifecycle = EncounterRuntimeLifecycle.ActiveCombat;
            _armed = false;
        }

        internal void ReleaseReservation(UnityEngine.Object requester)
        {
            if (encounterGroup != null)
            {
                encounterGroup.ReleaseReservation(requester);
                return;
            }

            if (_lifecycle != EncounterRuntimeLifecycle.StartReserved || _reservationOwner != requester)
                return;

            _reservationOwner = null;
            _lifecycle = EncounterRuntimeLifecycle.Idle;
            _armed = true;
        }

        internal void AdoptAcceptedSession(string completionId)
        {
            if (encounterGroup != null)
            {
                encounterGroup.AdoptAcceptedSession(completionId);
                return;
            }

            if (_lifecycle == EncounterRuntimeLifecycle.Cleared || string.IsNullOrWhiteSpace(completionId))
                return;

            if (_lifecycle == EncounterRuntimeLifecycle.ActiveCombat && _activeCompletionId == completionId)
                return;

            if (_lifecycle != EncounterRuntimeLifecycle.Idle && _lifecycle != EncounterRuntimeLifecycle.StartReserved)
                return;

            _reservationOwner = null;
            _activeCompletionId = completionId;
            _processedCompletionId = null;
            _lifecycle = EncounterRuntimeLifecycle.ActiveCombat;
            _armed = false;
        }

        internal bool TryBeginOutcome(CombatResult result)
        {
            if (encounterGroup != null)
                return encounterGroup.TryBeginOutcome(result);

            if (result == null || _lifecycle != EncounterRuntimeLifecycle.ActiveCombat)
                return false;

            if (!string.IsNullOrWhiteSpace(_activeCompletionId) && result.CompletionId != _activeCompletionId)
                return false;

            string completionId = !string.IsNullOrWhiteSpace(result.CompletionId) ? result.CompletionId : _activeCompletionId;
            if (_processedCompletionId == completionId)
                return false;

            _processedCompletionId = completionId;
            _lifecycle = EncounterRuntimeLifecycle.AwaitingPostCombat;
            return true;
        }

        internal void CompleteOutcome(CombatResult result, bool hasActiveEnemyMembers)
        {
            if (encounterGroup != null)
            {
                encounterGroup.CompleteOutcome(result, hasActiveEnemyMembers);
                return;
            }

            if (_lifecycle != EncounterRuntimeLifecycle.AwaitingPostCombat)
                return;

            bool victory = result != null &&
                           (result.EndReason != CombatEndReason.None
                               ? result.EndReason == CombatEndReason.Victory
                               : result.IsWin);
            _lifecycle = victory && !hasActiveEnemyMembers
                ? EncounterRuntimeLifecycle.Cleared
                : EncounterRuntimeLifecycle.RearmPending;
            _armed = _lifecycle == EncounterRuntimeLifecycle.Idle;
        }

        internal void ObserveExploration()
        {
            if (encounterGroup != null)
            {
                encounterGroup.ObserveExploration();
                _armed = encounterGroup.Lifecycle == EncounterRuntimeLifecycle.Idle;
                return;
            }

            _explorationObserved = true;
            TryRearm();
        }

        internal void RegisterPlayerCollider(Collider2D collider)
        {
            if (encounterGroup != null)
            {
                encounterGroup.RegisterPlayerCollider(collider);
                return;
            }

            if (collider != null)
                _playerColliderIds.Add(collider.GetInstanceID());
        }

        internal void UnregisterPlayerCollider(Collider2D collider)
        {
            if (encounterGroup != null)
            {
                encounterGroup.UnregisterPlayerCollider(collider);
                _armed = encounterGroup.Lifecycle == EncounterRuntimeLifecycle.Idle;
                return;
            }

            if (collider != null)
                _playerColliderIds.Remove(collider.GetInstanceID());

            TryRearm();
        }

        private void TryRearm()
        {
            if (_lifecycle != EncounterRuntimeLifecycle.RearmPending || !_explorationObserved || HasPlayerPresence)
                return;

            _activeCompletionId = null;
            _processedCompletionId = null;
            _explorationObserved = false;
            _lifecycle = EncounterRuntimeLifecycle.Idle;
            _armed = true;
        }

        EncounterRuntimeLifecycle ICombatEncounterRuntimeOwner.Lifecycle => Lifecycle;
        string ICombatEncounterRuntimeOwner.ActiveCompletionId => ActiveCompletionId;
        bool ICombatEncounterRuntimeOwner.HasPlayerPresence => HasPlayerPresence;
        bool ICombatEncounterRuntimeOwner.TryReserve(UnityEngine.Object requester) => TryReserve(requester);
        void ICombatEncounterRuntimeOwner.CommitReservation(string completionId) => CommitReservation(completionId);
        void ICombatEncounterRuntimeOwner.ReleaseReservation(UnityEngine.Object requester) => ReleaseReservation(requester);
        void ICombatEncounterRuntimeOwner.AdoptAcceptedSession(string completionId) => AdoptAcceptedSession(completionId);
        bool ICombatEncounterRuntimeOwner.TryBeginOutcome(CombatResult result) => TryBeginOutcome(result);
        void ICombatEncounterRuntimeOwner.CompleteOutcome(CombatResult result, bool hasActiveEnemyMembers) => CompleteOutcome(result, hasActiveEnemyMembers);
        void ICombatEncounterRuntimeOwner.ObserveExploration() => ObserveExploration();
        void ICombatEncounterRuntimeOwner.RegisterPlayerCollider(Collider2D collider) => RegisterPlayerCollider(collider);
        void ICombatEncounterRuntimeOwner.UnregisterPlayerCollider(Collider2D collider) => UnregisterPlayerCollider(collider);

        private CombatStartRequest CreateEncounterRequest(List<GameObject> allies, List<GameObject> enemies)
        {
            CombatStartRequest request = new CombatStartRequest(
                startReason,
                initiativeSide,
                0,
                -1,
                openingEffectOrNull
            );

            AddValidObjects(request.AllyFieldObjects, allies);
            AddValidObjects(request.EnemyFieldObjects, enemies);
            request.EncounterOwnerOrNull = RuntimeOwner as UnityEngine.Object;
            return request;
        }

        private static void AddValidObjects(List<GameObject> destination, List<GameObject> source)
        {
            if (destination == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy && !destination.Contains(candidate))
                    destination.Add(candidate);
            }
        }

        private bool HasPlayerTag(Collider2D other)
        {
            if (other == null)
            {
                LogDebug("Ignored trigger because collider is null.");
                return false;
            }

            if (string.IsNullOrEmpty(playerTag))
            {
                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because PlayerTag is empty.");
                return false;
            }

            try
            {
                if (other.CompareTag(playerTag))
                    return true;

                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because tag '{GetColliderTag(other)}' does not match PlayerTag '{playerTag}'.");
                return false;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"[CombatEncounterTrigger2D] PlayerTag '{playerTag}' is not defined. Trigger ignored. {exception.Message}", this);
                return false;
            }
        }

        private GameObject ResolvePlayerRoot(Collider2D other)
        {
            if (other == null)
                return null;

            PlayerInputController playerInput = other.GetComponentInParent<PlayerInputController>();
            if (playerInput != null)
                return playerInput.gameObject;

            CombatHpComponent hpComponent = other.GetComponentInParent<CombatHpComponent>();
            if (hpComponent != null)
                return hpComponent.gameObject;

            Transform root = other.transform != null ? other.transform.root : null;
            if (root != null && HasTag(root.gameObject, playerTag))
                return root.gameObject;

            return HasPlayerTag(other) ? other.gameObject : null;
        }

        private bool HasTag(GameObject target, string tagName)
        {
            if (target == null)
                return false;

            if (string.IsNullOrEmpty(tagName))
            {
                LogDebug($"Ignored '{target.name}' because PlayerTag is empty.");
                return false;
            }

            try
            {
                return target.CompareTag(tagName);
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"[CombatEncounterTrigger2D] PlayerTag '{tagName}' is not defined. Trigger ignored. {exception.Message}", this);
                return false;
            }
        }

        private string GetCurrentGameStateText()
        {
            return GameStateMachine.Instance != null ? GameStateMachine.Instance.Current.ToString() : "<none>";
        }

        private static string GetColliderName(Collider2D other)
        {
            return other != null && other.gameObject != null ? other.gameObject.name : "<null>";
        }

        private static string GetColliderTag(Collider2D other)
        {
            if (other == null || other.gameObject == null)
                return "<null>";

            try
            {
                return other.gameObject.tag;
            }
            catch (UnityException exception)
            {
                return $"<invalid tag: {exception.Message}>";
            }
        }

        private void LogDebug(string message)
        {
            if (!debugLog)
                return;

            Debug.Log($"[CombatEncounterTrigger2D] {message}", this);
        }
    }
}
