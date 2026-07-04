using System;
using UnityEngine;
using Game.DemoMission.Data;
using Game.Quest;

namespace Game.DemoMission.Runtime
{
    public sealed class DemoMissionRuntime : MonoBehaviour
    {
        public const string EnemyDefeatedObjectiveId = "enemy_defeated";
        public const string NpcTalkedObjectiveId = "npc_talked";
        public const string NpcRescuedObjectiveId = "npc_rescued";

        public static DemoMissionRuntime Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private DemoMissionDefinitionSO currentMission;

        public DemoMissionDefinitionSO CurrentMission => currentMission;
        public int CurrentEnemyKills => EnemyDefeatCount;
        public int EnemyDefeatCount { get; private set; }
        public bool IsNpcRescued { get; private set; }

        public event Action OnMissionProgressChanged;
        public event Action OnMissionCompleted;

        private bool _completionRaised;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        public static DemoMissionRuntime GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            DemoMissionRuntime runtime = FindFirstObjectByType<DemoMissionRuntime>();
            if (runtime != null)
                return runtime;

            GameObject go = new GameObject("DemoMissionRuntime");
            return go.AddComponent<DemoMissionRuntime>();
        }

        public void SetCurrentMission(DemoMissionDefinitionSO mission)
        {
            SetMission(mission);
            ResetMissionProgress();
        }

        public void SetMission(DemoMissionDefinitionSO mission)
        {
            currentMission = mission;
        }

        public void ResetMissionProgress()
        {
            EnemyDefeatCount = 0;
            IsNpcRescued = false;
            _completionRaised = false;
            RaiseProgressChanged();
        }

        public void RegisterEnemyDefeated()
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] Enemy defeat ignored. Current mission is null.", this);
                return;
            }

            int previousKills = EnemyDefeatCount;
            int requiredKills = Mathf.Max(0, currentMission.requiredEnemyKills);
            if (requiredKills > 0)
                EnemyDefeatCount = Mathf.Min(EnemyDefeatCount + 1, requiredKills);
            else
                EnemyDefeatCount++;

            if (EnemyDefeatCount != previousKills)
                PublishQuestEvent(QuestEventType.Kill, EnemyDefeatedObjectiveId, 1);

            RaiseProgressChanged();
            TryRaiseCompleted();
        }

        public void RegisterNpcRescued()
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] NPC rescue ignored. Current mission is null.", this);
                return;
            }

            if (IsNpcRescued)
                return;

            IsNpcRescued = true;
            PublishQuestEvent(QuestEventType.Rescue, NpcRescuedObjectiveId, 1);
            RaiseProgressChanged();
            TryRaiseCompleted();
        }

        public bool HasRequiredEnemyKills()
        {
            if (currentMission == null)
                return false;

            return EnemyDefeatCount >= Mathf.Max(0, currentMission.requiredEnemyKills);
        }

        public bool IsMissionComplete()
        {
            return currentMission != null && HasRequiredEnemyKills() && IsNpcRescued;
        }

        private void TryRaiseCompleted()
        {
            if (_completionRaised || !IsMissionComplete())
                return;

            _completionRaised = true;
            OnMissionCompleted?.Invoke();
        }

        private void RaiseProgressChanged()
        {
            OnMissionProgressChanged?.Invoke();
        }

        public void PublishQuestEvent(QuestEventType eventType, string objectiveId, int amount = 1)
        {
            if (currentMission == null)
                return;

            string questId = !string.IsNullOrWhiteSpace(currentMission.missionId)
                ? currentMission.missionId
                : currentMission.name;

            QuestEventChannel.Publish(new QuestEvent(eventType, questId, objectiveId, amount, gameObject));
        }
    }
}
