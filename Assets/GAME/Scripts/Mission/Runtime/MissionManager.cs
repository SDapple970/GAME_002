// Assets/GAME/Scripts/Mission/Runtime/MissionManager.cs
using System;
using System.Collections.Generic;
using Game.Mission.Data;
using UnityEngine;

namespace Game.Mission
{
    public sealed class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        private readonly List<MissionDefinitionSO> activeMissions = new();
        private readonly HashSet<string> completedMissionIds = new();
        private readonly Dictionary<string, HashSet<string>> completedObjectivesByMission = new();

        public event Action OnMissionStateChanged;
        public event Action<MissionDefinitionSO> OnMissionCompleted;

        public IReadOnlyList<MissionDefinitionSO> ActiveMissions => activeMissions;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartMission(MissionDefinitionSO mission)
        {
            if (mission == null) return;

            string missionId = mission.MissionId;
            if (string.IsNullOrEmpty(missionId))
            {
                Debug.LogWarning("[MissionManager] Mission id is empty. StartMission ignored.", mission);
                return;
            }

            if (IsMissionCompleted(missionId) || IsMissionActive(missionId)) return;

            activeMissions.Add(mission);
            NotifyChanged();
        }

        public void CompleteObjective(string missionId, string objectiveId)
        {
            MissionDefinitionSO mission = GetActiveMission(missionId);
            if (mission == null)
            {
                Debug.LogWarning($"[MissionManager] CompleteObjective ignored. Mission is not active. missionId='{missionId}' objectiveId='{objectiveId}'.");
                return;
            }

            if (string.IsNullOrEmpty(objectiveId))
            {
                Debug.LogWarning($"[MissionManager] CompleteObjective ignored. Objective id is empty. missionId='{missionId}'.");
                return;
            }

            HashSet<string> completedObjectives = GetOrCreateCompletedObjectives(missionId);
            bool changed = completedObjectives.Add(objectiveId);

            if (mission.AutoCompleteWhenAllObjectivesComplete && AreAllRequiredObjectivesComplete(mission))
            {
                CompleteMission(missionId);
                return;
            }

            if (changed)
            {
                NotifyChanged();
            }
        }

        public void CompleteMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return;

            MissionDefinitionSO completedMission = GetActiveMission(missionId);
            for (int i = activeMissions.Count - 1; i >= 0; i--)
            {
                MissionDefinitionSO mission = activeMissions[i];
                if (mission != null && mission.MissionId == missionId)
                {
                    activeMissions.RemoveAt(i);
                }
            }

            bool newlyCompleted = completedMissionIds.Add(missionId);
            if (newlyCompleted && completedMission != null)
                OnMissionCompleted?.Invoke(completedMission);

            NotifyChanged();
        }

        public bool IsMissionActive(string missionId)
        {
            return GetActiveMission(missionId) != null;
        }

        public bool IsMissionCompleted(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return false;
            return completedMissionIds.Contains(missionId);
        }

        public bool IsObjectiveCompleted(string missionId, string objectiveId)
        {
            if (string.IsNullOrEmpty(missionId) || string.IsNullOrEmpty(objectiveId)) return false;
            return completedObjectivesByMission.TryGetValue(missionId, out HashSet<string> objectives) && objectives.Contains(objectiveId);
        }

        public MissionDefinitionSO GetActiveMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return null;

            foreach (MissionDefinitionSO mission in activeMissions)
            {
                if (mission != null && mission.MissionId == missionId)
                {
                    return mission;
                }
            }

            return null;
        }

        private bool AreAllRequiredObjectivesComplete(MissionDefinitionSO mission)
        {
            if (mission == null || mission.Objectives == null) return false;

            foreach (MissionObjective objective in mission.Objectives)
            {
                if (objective == null || objective.Optional) continue;

                if (!IsObjectiveCompleted(mission.MissionId, objective.ObjectiveId))
                {
                    return false;
                }
            }

            return true;
        }

        private HashSet<string> GetOrCreateCompletedObjectives(string missionId)
        {
            if (!completedObjectivesByMission.TryGetValue(missionId, out HashSet<string> objectives))
            {
                objectives = new HashSet<string>();
                completedObjectivesByMission[missionId] = objectives;
            }

            return objectives;
        }

        private void NotifyChanged()
        {
            OnMissionStateChanged?.Invoke();
        }
    }
}
