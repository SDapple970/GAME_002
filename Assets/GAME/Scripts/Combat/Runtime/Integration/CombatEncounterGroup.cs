using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Integration
{
    public sealed class CombatEncounterGroup : MonoBehaviour, ICombatEncounterRuntimeOwner
    {
        [SerializeField] private bool autoCollectChildren = true;
        [SerializeField] private List<GameObject> enemies = new();

        private readonly HashSet<int> _warnedInvalidAutoChildren = new();
        private readonly HashSet<int> _warnedInvalidManualMembers = new();
        private readonly HashSet<int> _playerColliderIds = new();
        private Object _reservationOwner;
        private EncounterRuntimeLifecycle _lifecycle;
        private string _activeCompletionId;
        private string _processedCompletionId;
        private bool _explorationObserved;

        internal EncounterRuntimeLifecycle Lifecycle => _lifecycle;
        internal string ActiveCompletionId => _activeCompletionId;
        internal bool HasPlayerPresence => _playerColliderIds.Count > 0;

        public List<GameObject> GetActiveEnemies()
        {
            List<GameObject> result = new List<GameObject>();
            HashSet<GameObject> seen = new HashSet<GameObject>();

            bool hasManualMembers = HasManualMembers();
            if (!autoCollectChildren || hasManualMembers)
            {
                AddManualActiveUnique(result, seen);
                return result;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                GameObject candidate = child != null ? child.gameObject : null;
                if (candidate == null || !candidate.activeInHierarchy || !seen.Add(candidate))
                    continue;

                HpAccessor accessor = HpAccessor.TryCreate(candidate);
                if (accessor != null && accessor.IsValid)
                {
                    result.Add(candidate);
                    continue;
                }

                int instanceId = candidate.GetInstanceID();
                if (_warnedInvalidAutoChildren.Add(instanceId))
                {
                    Debug.LogWarning(
                        $"[CombatEncounterGroup] Auto-collected child '{candidate.name}' is not a field combatant and was excluded. " +
                        "Add a valid HP source or keep helper objects outside the combatant roster.",
                        this);
                }
            }

            return result;
        }

        internal bool TryReserve(Object requester)
        {
            if (requester == null || _lifecycle != EncounterRuntimeLifecycle.Idle)
                return false;

            _reservationOwner = requester;
            _lifecycle = EncounterRuntimeLifecycle.StartReserved;
            _explorationObserved = false;
            return true;
        }

        internal void CommitReservation(string completionId)
        {
            if (_lifecycle != EncounterRuntimeLifecycle.StartReserved &&
                _lifecycle != EncounterRuntimeLifecycle.ActiveCombat)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(completionId))
                return;

            _reservationOwner = null;
            _activeCompletionId = completionId;
            _processedCompletionId = null;
            _lifecycle = EncounterRuntimeLifecycle.ActiveCombat;
        }

        internal void ReleaseReservation(Object requester)
        {
            if (_lifecycle != EncounterRuntimeLifecycle.StartReserved || _reservationOwner != requester)
                return;

            _reservationOwner = null;
            _lifecycle = EncounterRuntimeLifecycle.Idle;
        }

        internal void AdoptAcceptedSession(string completionId)
        {
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
        }

        internal bool TryBeginOutcome(CombatResult result)
        {
            if (result == null || _lifecycle != EncounterRuntimeLifecycle.ActiveCombat)
                return false;

            if (!string.IsNullOrWhiteSpace(_activeCompletionId) && result.CompletionId != _activeCompletionId)
                return false;

            string completionId = !string.IsNullOrWhiteSpace(result.CompletionId)
                ? result.CompletionId
                : _activeCompletionId;
            if (_processedCompletionId == completionId)
                return false;

            _processedCompletionId = completionId;
            _lifecycle = EncounterRuntimeLifecycle.AwaitingPostCombat;
            return true;
        }

        internal void CompleteOutcome(CombatResult result, bool hasActiveEnemyMembers)
        {
            if (_lifecycle != EncounterRuntimeLifecycle.AwaitingPostCombat)
                return;

            bool victory = result != null &&
                           (result.EndReason != CombatEndReason.None
                               ? result.EndReason == CombatEndReason.Victory
                               : result.IsWin);
            _lifecycle = victory && !hasActiveEnemyMembers
                ? EncounterRuntimeLifecycle.Cleared
                : EncounterRuntimeLifecycle.RearmPending;
            _reservationOwner = null;
        }

        internal void ObserveExploration()
        {
            _explorationObserved = true;
            TryRearm();
        }

        internal void RegisterPlayerCollider(Collider2D collider)
        {
            if (collider != null)
                _playerColliderIds.Add(collider.GetInstanceID());
        }

        internal void UnregisterPlayerCollider(Collider2D collider)
        {
            if (collider != null)
                _playerColliderIds.Remove(collider.GetInstanceID());

            TryRearm();
        }

        private void TryRearm()
        {
            if (_lifecycle != EncounterRuntimeLifecycle.RearmPending ||
                !_explorationObserved ||
                HasPlayerPresence)
            {
                return;
            }

            _activeCompletionId = null;
            _processedCompletionId = null;
            _explorationObserved = false;
            _lifecycle = EncounterRuntimeLifecycle.Idle;
        }

        EncounterRuntimeLifecycle ICombatEncounterRuntimeOwner.Lifecycle => Lifecycle;
        string ICombatEncounterRuntimeOwner.ActiveCompletionId => ActiveCompletionId;
        bool ICombatEncounterRuntimeOwner.HasPlayerPresence => HasPlayerPresence;
        bool ICombatEncounterRuntimeOwner.TryReserve(Object requester) => TryReserve(requester);
        void ICombatEncounterRuntimeOwner.CommitReservation(string completionId) => CommitReservation(completionId);
        void ICombatEncounterRuntimeOwner.ReleaseReservation(Object requester) => ReleaseReservation(requester);
        void ICombatEncounterRuntimeOwner.AdoptAcceptedSession(string completionId) => AdoptAcceptedSession(completionId);
        bool ICombatEncounterRuntimeOwner.TryBeginOutcome(CombatResult result) => TryBeginOutcome(result);
        void ICombatEncounterRuntimeOwner.CompleteOutcome(CombatResult result, bool hasActiveEnemyMembers) => CompleteOutcome(result, hasActiveEnemyMembers);
        void ICombatEncounterRuntimeOwner.ObserveExploration() => ObserveExploration();
        void ICombatEncounterRuntimeOwner.RegisterPlayerCollider(Collider2D collider) => RegisterPlayerCollider(collider);
        void ICombatEncounterRuntimeOwner.UnregisterPlayerCollider(Collider2D collider) => UnregisterPlayerCollider(collider);

        private bool HasManualMembers()
        {
            if (enemies == null)
                return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                    return true;
            }

            return false;
        }

        private void AddManualActiveUnique(
            List<GameObject> destination,
            HashSet<GameObject> seen)
        {
            if (enemies == null)
                return;

            for (int i = 0; i < enemies.Count; i++)
            {
                GameObject candidate = enemies[i];
                if (candidate == null || !candidate.activeInHierarchy || !seen.Add(candidate))
                    continue;

                destination.Add(candidate);
                HpAccessor accessor = HpAccessor.TryCreate(candidate);
                if ((accessor == null || !accessor.IsValid) &&
                    _warnedInvalidManualMembers.Add(candidate.GetInstanceID()))
                {
                    Debug.LogWarning(
                        $"[CombatEncounterGroup] Manually authored member '{candidate.name}' cannot currently be adapted as a combatant. " +
                        "It remains in the snapshot so CombatEntryPoint can reject the authored encounter atomically.",
                        this);
                }
            }
        }
    }
}
