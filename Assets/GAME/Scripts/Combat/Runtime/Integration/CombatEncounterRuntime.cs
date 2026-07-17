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
}
