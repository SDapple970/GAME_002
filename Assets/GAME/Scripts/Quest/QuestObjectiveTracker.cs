using System.Collections.Generic;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestObjectiveTracker : MonoBehaviour
    {
        private static readonly Dictionary<int, QuestObjectiveTracker> OwnersByRuntimeId = new();

        [SerializeField] private QuestRuntime questRuntime;

        private QuestRuntime _claimedRuntime;
        private bool _subscribed;
        private bool _duplicateOwnerWarned;
        private bool _missingRuntimeWarned;

        private void Awake()
        {
            ResolveRuntime();
        }

        private void OnEnable()
        {
            ResolveRuntime();
            TryClaimAndSubscribe();
        }

        private void OnDisable()
        {
            UnsubscribeAndRelease();
        }

        private void OnDestroy()
        {
            UnsubscribeAndRelease();
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteObjective(questId, objectiveId);
        }

        private void HandleQuestEvent(QuestEvent questEvent)
        {
            _claimedRuntime?.ApplyEvent(questEvent);
        }

        private void ResolveRuntime()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (questRuntime == null && !_missingRuntimeWarned)
            {
                _missingRuntimeWarned = true;
                Debug.LogWarning("[QuestObjectiveTracker] QuestRuntime is missing. QuestEventChannel delivery is disabled.", this);
            }
        }

        private void TryClaimAndSubscribe()
        {
            if (_subscribed || questRuntime == null)
                return;

            PruneDestroyedOwners();
            int key = questRuntime.GetInstanceID();
            if (OwnersByRuntimeId.TryGetValue(key, out QuestObjectiveTracker owner) && owner != this)
            {
                if (!_duplicateOwnerWarned)
                {
                    _duplicateOwnerWarned = true;
                    Debug.LogWarning(
                        $"[QuestObjectiveTracker] Duplicate tracker blocked for QuestRuntime '{questRuntime.name}'. Active='{owner.name}', Duplicate='{name}'.",
                        this);
                }

                return;
            }

            OwnersByRuntimeId[key] = this;
            _claimedRuntime = questRuntime;
            QuestEventChannel.OnEventRaised -= HandleQuestEvent;
            QuestEventChannel.OnEventRaised += HandleQuestEvent;
            _subscribed = true;
        }

        private void UnsubscribeAndRelease()
        {
            if (_subscribed)
                QuestEventChannel.OnEventRaised -= HandleQuestEvent;

            if (_claimedRuntime != null)
            {
                int key = _claimedRuntime.GetInstanceID();
                if (OwnersByRuntimeId.TryGetValue(key, out QuestObjectiveTracker owner) && owner == this)
                    OwnersByRuntimeId.Remove(key);
            }

            _subscribed = false;
            _claimedRuntime = null;
        }

        private static void PruneDestroyedOwners()
        {
            List<int> staleKeys = null;
            foreach (KeyValuePair<int, QuestObjectiveTracker> pair in OwnersByRuntimeId)
            {
                if (pair.Value != null)
                    continue;

                staleKeys ??= new List<int>();
                staleKeys.Add(pair.Key);
            }

            if (staleKeys == null)
                return;

            for (int i = 0; i < staleKeys.Count; i++)
                OwnersByRuntimeId.Remove(staleKeys[i]);
        }

        internal static void ResetOwnershipForTests()
        {
            OwnersByRuntimeId.Clear();
        }
    }
}
