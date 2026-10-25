using System;
using System.Collections.Generic;
using Game.Common.Identity;
using Game.Core;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Quest
{
    public sealed partial class QuestRuntime
    {
        private sealed class GeneralMissionRunState
        {
            public List<string> selectedTargetIds = new();
            public List<string> completedTargetIds = new();
            public List<string> healthOutcomeIds = new();
            public int rescueHp;
            public bool following;
            public bool escaped;
            public bool hasEscortPosition;
            public Vector3 escortPosition;
            public List<Vector3> escortPath = new();
        }
        private readonly Dictionary<string, GeneralMissionRunState> _generalMissions = new(StringComparer.Ordinal);
        public event Action<string> OnGeneralMissionChanged;

        public bool IsGeneralMission(string questId) => TryGetDefinition(questId, out QuestDefinitionSO definition) &&
            definition.GeneralMission != null && definition.GeneralMission.Kind != GeneralMissionKind.None;

        public bool TryAcceptGeneralMission(QuestDefinitionSO definition, out string reason)
        {
            reason = null;
            if (definition?.GeneralMission == null || !definition.GeneralMission.TryValidate(definition, out reason))
            { reason ??= "A valid general mission definition is required."; return false; }
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
            { reason = "Mission acceptance requires Exploration."; return false; }
            // A field run has one target roster and one exit. Do not overlap missions.
            foreach (RuntimeQuestState state in _runtimeByQuestId.Values)
                if (state.Status == QuestStatus.Active && state.Definition?.GeneralMission?.Kind > GeneralMissionKind.None)
                { reason = "Finish the active general mission first."; return false; }
            QuestStatus status = GetQuestStatus(definition.QuestId);
            bool accepted;
            if (status == QuestStatus.Completed) accepted = TryStartNextRun(definition.QuestId);
            else if (status == QuestStatus.Failed) accepted = RetryQuest(definition.QuestId);
            else { StartQuest(definition); accepted = IsQuestActive(definition.QuestId); }
            if (!accepted) reason = "Mission result is pending, or the quest cannot be accepted.";
            return accepted;
        }

        private bool CanBeginGeneralMission(QuestDefinitionSO definition, out string reason)
        {
            reason = null;
            if (definition?.GeneralMission == null || definition.GeneralMission.Kind == GeneralMissionKind.None) return true;
            if (!definition.GeneralMission.TryValidate(definition, out reason)) return false;
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
            { reason = "General missions can only begin in Exploration."; return false; }
            foreach (RuntimeQuestState existing in _runtimeByQuestId.Values)
                if (existing.QuestId != definition.QuestId && existing.Status == QuestStatus.Active &&
                    existing.Definition?.GeneralMission?.Kind > GeneralMissionKind.None)
                { reason = "Another general mission is active."; return false; }
            return true;
        }

        public GeneralMissionSnapshot GetGeneralMissionSnapshot(string questId)
        {
            if (!_generalMissions.TryGetValue(questId ?? "", out GeneralMissionRunState state) ||
                !TryGetDefinition(questId, out QuestDefinitionSO definition) || definition.GeneralMission == null)
                return null;
            GeneralMissionDefinition rules = definition.GeneralMission;
            return new GeneralMissionSnapshot
            {
                QuestId = questId, RunId = GetQuestRunId(questId), Attempt = GetQuestAttempt(questId),
                Kind = rules.Kind, Status = GetQuestStatus(questId), Progress = GetObjectiveProgress(questId, rules.ObjectiveId),
                Required = rules.RequiredCount, RescueHp = state.rescueHp, RescueMaxHp = rules.RescueMaxHp,
                Following = state.following, Escaped = state.escaped,
                ExplorationPercent = rules.Kind == GeneralMissionKind.Exploration && rules.TargetIds.Count > 0
                    ? (int)((long)state.completedTargetIds.Count * 100 / rules.TargetIds.Count) : 0,
                SelectedTargetIds = new List<string>(state.selectedTargetIds).AsReadOnly(),
                CompletedTargetIds = new List<string>(state.completedTargetIds).AsReadOnly()
            };
        }

        public bool TryExploreMissionCell(string questId, string runId, int attempt, string cellId) =>
            TryRecordGeneralTarget(questId, runId, attempt, cellId, GeneralMissionKind.Exploration);

        public bool TryClearMissionEncounter(string questId, string runId, int attempt, string encounterId) =>
            TryRecordGeneralTarget(questId, runId, attempt, encounterId, GeneralMissionKind.Extermination);

        public bool TryActivateMissionSuppressor(string questId, string runId, int attempt, string deviceId) =>
            TryRecordGeneralTarget(questId, runId, attempt, deviceId, GeneralMissionKind.Suppressors);

        private bool TryRecordGeneralTarget(string questId, string runId, int attempt, string targetId, GeneralMissionKind kind)
        {
            if (!TryGetGeneralRun(questId, runId, attempt, kind, out RuntimeQuestState owner,
                    out GeneralMissionRunState state) || string.IsNullOrWhiteSpace(targetId) ||
                !state.selectedTargetIds.Contains(targetId) || state.completedTargetIds.Contains(targetId)) return false;
            state.completedTargetIds.Add(targetId);
            AdvanceGeneralMission(owner, state.completedTargetIds.Count);
            return true;
        }

        public bool TryRescueMissionTarget(string questId, string runId, int attempt, string targetId)
        {
            if (!TryGetGeneralRun(questId, runId, attempt, GeneralMissionKind.Rescue, out RuntimeQuestState owner,
                    out GeneralMissionRunState state) || owner.Definition.GeneralMission.RescueTargetId != targetId ||
                state.following || state.rescueHp <= 0) return false;
            state.following = true;
            OnGeneralMissionChanged?.Invoke(questId);
            return true;
        }

        public bool TryChangeMissionRescueHp(string questId, string runId, int attempt, int delta, string outcomeId)
        {
            if (!TryGetGeneralRun(questId, runId, attempt, GeneralMissionKind.Rescue, out RuntimeQuestState owner,
                    out GeneralMissionRunState state) || !state.following || delta == 0 ||
                string.IsNullOrWhiteSpace(outcomeId) || state.healthOutcomeIds.Contains(outcomeId)) return false;
            int next = (int)Math.Clamp((long)state.rescueHp + delta, 0, owner.Definition.GeneralMission.RescueMaxHp);
            if (next == state.rescueHp) return false;
            state.healthOutcomeIds.Add(outcomeId);
            state.rescueHp = next;
            if (next == 0)
            {
                state.following = false;
                FailQuest(questId, new GameplayOutcomeIdentity(GameplayOutcomeSourceType.QuestCompletion,
                    $"quest:{questId}", $"run:{runId}:attempt:{attempt}:rescue-death"), "rescue_target_dead", runId);
            }
            OnGeneralMissionChanged?.Invoke(questId);
            return true;
        }

        public bool TryExitRescueMission(string questId, string runId, int attempt, string exitId)
        {
            if (!TryGetGeneralRun(questId, runId, attempt, GeneralMissionKind.Rescue, out RuntimeQuestState owner,
                    out GeneralMissionRunState state) || !state.following || state.escaped || state.rescueHp <= 0 ||
                owner.Definition.GeneralMission.ExitId != exitId ||
                (long)state.rescueHp * 100 < (long)owner.Definition.GeneralMission.RescueMaxHp * 80) return false;
            state.escaped = true;
            state.following = false;
            AdvanceGeneralMission(owner, 1);
            return true;
        }

        public bool UpdateMissionEscortPosition(string questId, string runId, int attempt, Vector3 position)
            => UpdateMissionEscortPosition(questId, runId, attempt, position, null);

        public bool UpdateMissionEscortPosition(string questId, string runId, int attempt, Vector3 position, IReadOnlyList<Vector3> path)
        {
            if (!TryGetGeneralRun(questId, runId, attempt, GeneralMissionKind.Rescue, out _, out GeneralMissionRunState state) ||
                !state.following || !IsFinite(position)) return false;
            if (path != null)
            {
                if (path.Count > 4096) return false;
                for (int i = 0; i < path.Count; i++) if (!IsFinite(path[i])) return false;
                state.escortPath.Clear();
                for (int i = 0; i < path.Count; i++) state.escortPath.Add(path[i]);
            }
            state.hasEscortPosition = true; state.escortPosition = position; return true;
        }

        public IReadOnlyList<Vector3> GetMissionEscortPath(string questId) =>
            _generalMissions.TryGetValue(questId ?? "", out GeneralMissionRunState state)
                ? new List<Vector3>(state.escortPath).AsReadOnly() : Array.Empty<Vector3>();

        public bool TryGetMissionEscortPosition(string questId, out Vector3 position)
        {
            position = default;
            if (!_generalMissions.TryGetValue(questId ?? "", out GeneralMissionRunState state) || !state.hasEscortPosition) return false;
            position = state.escortPosition; return true;
        }

        private static bool IsFinite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        private bool TryGetGeneralRun(string questId, string runId, int attempt, GeneralMissionKind kind,
            out RuntimeQuestState owner, out GeneralMissionRunState state)
        {
            owner = null; state = null;
            return questId != null && _runtimeByQuestId.TryGetValue(questId, out owner) &&
                owner.Status == QuestStatus.Active && owner.Definition?.GeneralMission?.Kind == kind &&
                !string.IsNullOrWhiteSpace(runId) && owner.RunId == runId && owner.Attempt == attempt &&
                _generalMissions.TryGetValue(questId, out state);
        }

        private void AdvanceGeneralMission(RuntimeQuestState owner, int count)
        {
            GeneralMissionDefinition rules = owner.Definition.GeneralMission;
            ApplyProgressChange(owner, rules.ObjectiveId, Mathf.Min(count, rules.RequiredCount), rules.RequiredCount);
            OnGeneralMissionChanged?.Invoke(owner.QuestId);
        }

        private void BeginGeneralMission(RuntimeQuestState owner)
        {
            GeneralMissionDefinition rules = owner.Definition?.GeneralMission;
            _generalMissions.Remove(owner.QuestId);
            if (rules == null || rules.Kind == GeneralMissionKind.None || string.IsNullOrWhiteSpace(owner.RunId)) return;
            GeneralMissionRunState state = new() { rescueHp = rules.RescueMaxHp };
            state.selectedTargetIds.AddRange(rules.TargetIds);
            if (rules.Kind == GeneralMissionKind.Suppressors)
            {
                // Stable across retry/load; a new RunId produces a fresh selection.
                uint seed = 2166136261;
                foreach (char value in owner.RunId) seed = unchecked((seed ^ value) * 16777619);
                System.Random random = new(unchecked((int)seed));
                for (int i = state.selectedTargetIds.Count - 1; i > 0; i--)
                {
                    int other = random.Next(i + 1);
                    (state.selectedTargetIds[i], state.selectedTargetIds[other]) =
                        (state.selectedTargetIds[other], state.selectedTargetIds[i]);
                }
                if (state.selectedTargetIds.Count > 3) state.selectedTargetIds.RemoveRange(3, state.selectedTargetIds.Count - 3);
            }
            _generalMissions[owner.QuestId] = state;
        }

        private GeneralMissionStateSaveData CaptureGeneralMission(string questId) =>
            _generalMissions.TryGetValue(questId, out GeneralMissionRunState state) ? CloneGeneralState(state) : null;

        private static GeneralMissionStateSaveData CloneGeneralState(GeneralMissionRunState state) => new()
        {
            selectedTargetIds = new(state.selectedTargetIds ?? new()),
            completedTargetIds = new(state.completedTargetIds ?? new()),
            healthOutcomeIds = new(state.healthOutcomeIds ?? new()),
            rescueHp = state.rescueHp, following = state.following, escaped = state.escaped,
            hasEscortPosition = state.hasEscortPosition, escortPosition = state.escortPosition,
            escortPath = new(state.escortPath)
        };

        private void RestoreGeneralMission(RuntimeQuestState owner, GeneralMissionStateSaveData saved)
        {
            GeneralMissionDefinition rules = owner.Definition?.GeneralMission;
            if (rules == null || rules.Kind == GeneralMissionKind.None || owner.Status == QuestStatus.Inactive) return;
            BeginGeneralMission(owner);
            if (!_generalMissions.TryGetValue(owner.QuestId, out GeneralMissionRunState state)) return;
            if (saved == null) return;
            // Restore the roster chosen at acceptance, never roll new random locations on load.
            List<string> selected = saved.selectedTargetIds ?? new();
            HashSet<string> valid = new(StringComparer.Ordinal);
            foreach (string id in selected) if (id != null && Contains(rules.TargetIds, id)) valid.Add(id);
            if (rules.Kind == GeneralMissionKind.Suppressors && valid.Count == 3)
                state.selectedTargetIds = new List<string>(selected);
            foreach (string id in saved.completedTargetIds ?? new())
                if (state.selectedTargetIds.Contains(id) && !state.completedTargetIds.Contains(id)) state.completedTargetIds.Add(id);
            state.rescueHp = Mathf.Clamp(saved.rescueHp, 0, rules.RescueMaxHp);
            state.following = saved.following && state.rescueHp > 0 && owner.Status == QuestStatus.Active;
            state.escaped = saved.escaped && owner.Status == QuestStatus.Completed;
            state.healthOutcomeIds = new List<string>(new HashSet<string>(saved.healthOutcomeIds ?? new(), StringComparer.Ordinal));
            state.hasEscortPosition = saved.hasEscortPosition && IsFinite(saved.escortPosition);
            state.escortPosition = saved.escortPosition;
            foreach (Vector3 point in saved.escortPath ?? new())
                if (IsFinite(point) && state.escortPath.Count < 4096) state.escortPath.Add(point);
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        { for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true; return false; }
    }
}
