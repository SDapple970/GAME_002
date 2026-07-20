using System;
using UnityEngine;
using Game.DemoMission.Data;
using Game.NonCombat.Save;
using Game.Quest;

namespace Game.DemoMission.Runtime
{
    public sealed class DemoMissionRuntime : MonoBehaviour, ISaveDataProvider
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
        public bool IsQuestRuntimeAuthoritative => TryUseQuestRuntimeProgress();
        public int CurrentEnemyKills => EnemyDefeatCount;
        public int EnemyDefeatCount
        {
            get
            {
                if (TryUseQuestRuntimeProgress())
                    return questRuntime.GetObjectiveProgress(CurrentQuestId, EnemyDefeatedObjectiveId);
                return _enemyDefeatCount;
            }
            private set => _enemyDefeatCount = value;
        }
        public bool IsNpcRescued
        {
            get
            {
                if (TryUseQuestRuntimeProgress())
                    return questRuntime.GetObjectiveProgress(CurrentQuestId, NpcRescuedObjectiveId) > 0;
                return _isNpcRescued;
            }
            private set => _isNpcRescued = value;
        }

        public event Action OnMissionProgressChanged;
        public event Action OnMissionCompleted;

        private bool _completionRaised;
        private bool _missingQuestRuntimeWarned;
        private int _enemyDefeatCount;
        private bool _isNpcRescued;
        private int _compatibilityEventSequence;
        private QuestRuntime _subscribedQuestRuntime;

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

        private void OnEnable()
        {
            ConfigureQuestRuntime();
            SubscribeQuestRuntime();
        }

        private void OnDisable()
        {
            UnsubscribeQuestRuntime();
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
            RegisterEnemyDefeated($"demo:{CurrentQuestId}:enemy:{++_compatibilityEventSequence}");
        }

        public void RegisterEnemyDefeated(string eventId)
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] Enemy defeat ignored. Current mission is null.", this);
                return;
            }

            if (TryResolveQuestRuntime(false))
            {
                TryPublishQuestEvent(QuestEventType.Kill, EnemyDefeatedObjectiveId, 1, eventId);
                return;
            }

            int previousKills = _enemyDefeatCount;
            int requiredKills = Mathf.Max(0, currentMission.requiredEnemyKills);
            if (requiredKills > 0)
                _enemyDefeatCount = Mathf.Min(_enemyDefeatCount + 1, requiredKills);
            else
                _enemyDefeatCount++;

            if (_enemyDefeatCount != previousKills)
                RaiseProgressChanged();
            TryRaiseCompleted();
        }

        public void RegisterNpcRescued()
        {
            RegisterNpcRescued($"demo:{CurrentQuestId}:rescue:{GetInstanceID()}");
        }

        public void RegisterNpcRescued(string eventId)
        {
            if (currentMission == null)
            {
                Debug.LogWarning("[DemoMissionRuntime] NPC rescue ignored. Current mission is null.", this);
                return;
            }

            if (TryResolveQuestRuntime(false))
            {
                TryPublishQuestEvent(QuestEventType.Rescue, NpcRescuedObjectiveId, 1, eventId);
                return;
            }

            if (_isNpcRescued)
                return;

            _isNpcRescued = true;
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

            return _enemyDefeatCount >= Mathf.Max(0, currentMission.requiredEnemyKills);
        }

        public bool IsMissionComplete()
        {
            if (TryUseQuestRuntimeProgress())
                return questRuntime.IsQuestComplete(CurrentQuestId);

            return currentMission != null && HasRequiredEnemyKills() && _isNpcRescued;
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
            TryPublishQuestEvent(
                eventType,
                objectiveId,
                amount,
                $"demo:{CurrentQuestId}:{objectiveId}:{++_compatibilityEventSequence}");
        }

        public bool TryPublishQuestEvent(QuestEventType eventType, string objectiveId, int amount, string eventId)
        {
            if (currentMission == null)
                return false;

            if (!bridgeToQuestRuntime)
                return false;

            QuestEvent questEvent = new QuestEvent(eventType, CurrentQuestId, objectiveId, amount, gameObject, eventId);
            if (TryResolveQuestRuntime(true))
            {
                SubscribeQuestRuntime();
                return questRuntime.ApplyEvent(questEvent);
            }

            QuestEventChannel.Publish(questEvent);
            return false;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.demoMission ??= new DemoMissionSaveData();
            saveData.demoMission.missionId = CurrentQuestId;
            saveData.demoMission.enemyDefeatCount = EnemyDefeatCount;
            saveData.demoMission.npcRescued = IsNpcRescued;
            saveData.demoMission.completed = _completionRaised;
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
            SubscribeQuestRuntime();
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

        private void SubscribeQuestRuntime()
        {
            QuestRuntime candidate = TryResolveQuestRuntime(false) ? questRuntime : null;
            if (_subscribedQuestRuntime == candidate)
                return;

            UnsubscribeQuestRuntime();
            _subscribedQuestRuntime = candidate;
            if (_subscribedQuestRuntime == null)
                return;

            _subscribedQuestRuntime.OnObjectiveProgressChanged += HandleQuestProgressChanged;
            _subscribedQuestRuntime.OnQuestCompleted += HandleQuestCompleted;
        }

        private void UnsubscribeQuestRuntime()
        {
            if (_subscribedQuestRuntime != null)
            {
                _subscribedQuestRuntime.OnObjectiveProgressChanged -= HandleQuestProgressChanged;
                _subscribedQuestRuntime.OnQuestCompleted -= HandleQuestCompleted;
            }

            _subscribedQuestRuntime = null;
        }

        private void HandleQuestProgressChanged(string questId, string objectiveId, int current, int required)
        {
            if (questId != CurrentQuestId)
                return;

            _enemyDefeatCount = questRuntime.GetObjectiveProgress(CurrentQuestId, EnemyDefeatedObjectiveId);
            _isNpcRescued = questRuntime.GetObjectiveProgress(CurrentQuestId, NpcRescuedObjectiveId) > 0;
            RaiseProgressChanged();
        }

        private void HandleQuestCompleted(string questId)
        {
            if (questId != CurrentQuestId || _completionRaised)
                return;

            _completionRaised = true;
            OnMissionCompleted?.Invoke();
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
