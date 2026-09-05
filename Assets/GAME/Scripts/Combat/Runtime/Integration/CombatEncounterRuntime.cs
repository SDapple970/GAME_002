using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Integration
{
    internal enum EncounterRuntimeLifecycle
    {
        Idle,
        StartReserved,
        ActiveCombat,
        AwaitingPostCombat,
        RearmPending,
        Cleared
    }

    internal interface ICombatEncounterRuntimeOwner
    {
        EncounterRuntimeLifecycle Lifecycle { get; }
        string ActiveCompletionId { get; }
        bool HasPlayerPresence { get; }

        bool TryReserve(Object requester);
        void CommitReservation(string completionId);
        void ReleaseReservation(Object requester);
        void AdoptAcceptedSession(string completionId);
        bool TryBeginOutcome(CombatResult result);
        void CompleteOutcome(CombatResult result, bool hasActiveEnemyMembers);
        void ObserveExploration();
        void RegisterPlayerCollider(Collider2D collider);
        void UnregisterPlayerCollider(Collider2D collider);
    }

    // Runtime-only field state captured before an encounter can hand control to combat.
    // It is deliberately not save data: WorldSaveData determines whether the encounter
    // is available, while this restores the in-session field presentation after a rewind.
    internal sealed class EncounterMemberRestoreState
    {
        private readonly GameObject _member;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Vector3 _localScale;
        private readonly bool _activeSelf;
        private readonly int _hp;
        private readonly bool _hasHp;
        private readonly Dictionary<Behaviour, bool> _behaviours = new();
        private readonly Dictionary<Collider2D, bool> _colliders = new();
        private readonly Dictionary<Rigidbody2D, bool> _bodySimulation = new();

        private EncounterMemberRestoreState(GameObject member)
        {
            _member = member;
            Transform transform = member.transform;
            _position = transform.position;
            _rotation = transform.rotation;
            _localScale = transform.localScale;
            _activeSelf = member.activeSelf;

            HpAccessor accessor = HpAccessor.TryCreate(member);
            _hasHp = accessor != null && accessor.IsValid;
            _hp = _hasHp ? accessor.GetHp() : 0;

            foreach (Behaviour behaviour in member.GetComponentsInChildren<Behaviour>(true))
                if (behaviour != null)
                    _behaviours.TryAdd(behaviour, behaviour.enabled);
            foreach (Collider2D collider in member.GetComponentsInChildren<Collider2D>(true))
                if (collider != null)
                    _colliders.TryAdd(collider, collider.enabled);
            foreach (Rigidbody2D body in member.GetComponentsInChildren<Rigidbody2D>(true))
                if (body != null)
                    _bodySimulation.TryAdd(body, body.simulated);
        }

        internal static EncounterMemberRestoreState Capture(GameObject member)
        {
            return member != null ? new EncounterMemberRestoreState(member) : null;
        }

        internal void Restore()
        {
            if (_member == null)
                return;

            Transform transform = _member.transform;
            transform.position = _position;
            transform.rotation = _rotation;
            transform.localScale = _localScale;
            _member.SetActive(_activeSelf);

            if (_hasHp)
            {
                HpAccessor accessor = HpAccessor.TryCreate(_member);
                if (accessor != null && accessor.IsValid)
                    accessor.SetHp(_hp);
            }

            foreach (KeyValuePair<Behaviour, bool> pair in _behaviours)
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            foreach (KeyValuePair<Collider2D, bool> pair in _colliders)
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            foreach (KeyValuePair<Rigidbody2D, bool> pair in _bodySimulation)
                if (pair.Key != null)
                {
                    pair.Key.simulated = pair.Value;
                    pair.Key.linearVelocity = Vector2.zero;
                    pair.Key.angularVelocity = 0f;
                }
        }
    }
}
