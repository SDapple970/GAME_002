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
        public const string MissionCompletedObjectiveId = "mission_completed";

        public static DemoMissionRuntime Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private DemoMissionDefinitionSO currentMission;
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private bool bridgeToQuestRuntime = true;
        [SerializeField] private bool requireNpcTalkForQuestCompletion;
        [SerializeField] private bool requireNpcRescueForQuestCompletion = true;

        public DemoMissionDefinitionSO CurrentMission => currentMission;
        public string CurrentQuestId => ResolveQuestId(currentMission);
        public int CurrentEnemyKills => EnemyDefeatCount;
        public int EnemyDefeatCount { get; private set; }
        public bool IsNpcRescued { get; private set; }

        public event Action OnMissionProgressChanged;
        public event Action OnMissionCompleted;

        private bool _completionRaised;
        private bool _missingQuestRuntimeWarned;

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
            ConfigureQuestRuntime();
        }

        public void ResetMissionProgress()
        {
            EnemyDefeatCount = 0;
            IsNpcRescued = false;
            _completionRaised = false;
            ResetQuestRuntimeProgress();
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

            if (TryUseQuestRuntimeProgress())
            {
                int requiredKillsFromQuest = questRuntime.GetObjectiveRequiredCount(CurrentQuestId, EnemyDefeatedObjectiveId);
                if (requiredKillsFromQuest > 0)
                    return questRuntime.GetObjectiveProgress(CurrentQuestId, EnemyDefeatedObjectiveId) >= requiredKillsFromQuest;
            }

            return EnemyDefeatCount >= Mathf.Max(0, currentMission.requiredEnemyKills);
        }

        public bool IsMissionComplete()
        {
            if (TryUseQuestRuntimeProgress())
                return questRuntime.IsQuestComplete(CurrentQuestId);

            return currentMission != null && HasRequiredEnemyKills() && IsNpcRescued;
        }

        private void TryRaiseCompleted()
        {
            if (_completionRaised || !IsMissionComplete())
                return;

            _completionRaised = true;
            PublishQuestEvent(QuestEventType.MissionCompleted, MissionCompletedObjectiveId, 1);
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

            if (!bridgeToQuestRuntime)
                return;

            QuestEvent questEvent = new QuestEvent(eventType, CurrentQuestId, objectiveId, amount, gameObject);
            if (TryResolveQuestRuntime(true))
            {
                questRuntime.ApplyEvent(questEvent);
                return;
            }

            QuestEventChannel.Publish(questEvent);
        }

        private void ConfigureQuestRuntime()
        {
            if (currentMission == null || !bridgeToQuestRuntime)
                return;

            if (!TryResolveQuestRuntime(true))
                return;

            questRuntime.ConfigureCompatibilityQuest(
                CurrentQuestId,
                currentMission.requiredEnemyKills,
                requireNpcTalkForQuestCompletion,
                requireNpcRescueForQuestCompletion
            );
        }

        private void ResetQuestRuntimeProgress()
        {
            if (currentMission == null || !bridgeToQuestRuntime)
                return;

            if (TryResolveQuestRuntime(false))
                questRuntime.ResetQuestProgress(CurrentQuestId);
        }

        private bool TryUseQuestRuntimeProgress()
        {
            return currentMission != null &&
                   bridgeToQuestRuntime &&
                   TryResolveQuestRuntime(false) &&
                   questRuntime.HasQuest(CurrentQuestId);
        }

        private bool TryResolveQuestRuntime(bool warnIfMissing)
        {
            if (!bridgeToQuestRuntime)
                return false;

            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (questRuntime != null)
                return true;

            if (warnIfMissing && !_missingQuestRuntimeWarned)
            {
                _missingQuestRuntimeWarned = true;
                Debug.LogWarning("[DemoMissionRuntime] QuestRuntime is missing while DemoMission-to-Quest bridge mode is enabled. Falling back to DemoMission progress.", this);
            }

            return false;
        }

        private static string ResolveQuestId(DemoMissionDefinitionSO mission)
        {
            if (mission == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(mission.missionId)
                ? mission.missionId
                : mission.name;
        }
    }
}
