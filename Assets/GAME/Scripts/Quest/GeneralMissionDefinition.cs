using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Quest
{
    public enum GeneralMissionKind { None, Exploration, Extermination, Suppressors, Rescue }

    /// <summary>Authoring only. Stable IDs refer to equal-area survey cells, encounters,
    /// or candidate device locations; no scene objects or mutable progress live here.</summary>
    [Serializable]
    public sealed class GeneralMissionDefinition
    {
        [SerializeField] private GeneralMissionKind kind;
        [SerializeField] private string objectiveId = "mission";
        [SerializeField] private string[] targetIds = Array.Empty<string>();
        [SerializeField] private string rescueTargetId = "rescue_target";
        [SerializeField] private string exitId = "exit";
        [Min(1)] [SerializeField] private int rescueMaxHp = 100;

        public GeneralMissionKind Kind => kind;
        public string ObjectiveId => objectiveId;
        public IReadOnlyList<string> TargetIds => Array.AsReadOnly(targetIds ?? Array.Empty<string>());
        public string RescueTargetId => rescueTargetId;
        public string ExitId => exitId;
        public int RescueMaxHp => Mathf.Max(1, rescueMaxHp);
        public int RequiredCount => kind switch
        {
            GeneralMissionKind.Exploration => (int)(((long)(targetIds?.Length ?? 0) * 9 + 9) / 10),
            GeneralMissionKind.Extermination => targetIds?.Length ?? 0,
            GeneralMissionKind.Suppressors => 3,
            GeneralMissionKind.Rescue => 1,
            _ => 0
        };

        public QuestEventType EventType => kind switch
        {
            GeneralMissionKind.Exploration => QuestEventType.Explore,
            GeneralMissionKind.Extermination => QuestEventType.ClearEncounter,
            GeneralMissionKind.Suppressors => QuestEventType.Interact,
            GeneralMissionKind.Rescue => QuestEventType.Escort,
            _ => QuestEventType.Unknown
        };

        public bool TryValidate(QuestDefinitionSO quest, out string reason)
        {
            reason = null;
            if (!Enum.IsDefined(typeof(GeneralMissionKind), kind) || kind == GeneralMissionKind.None)
                reason = "Select a general mission kind.";
            else if (quest == null || !quest.Repeatable || quest.MissionDayCost != 1 ||
                     quest.RetryPolicy != QuestRetryPolicy.RestartFromBeginning)
                reason = "General missions require Repeatable, one day, and RestartFromBeginning.";
            else if (string.IsNullOrWhiteSpace(objectiveId) || quest.Objectives == null || quest.Objectives.Length != 1)
                reason = "Author exactly one required mission objective.";
            else
            {
                QuestObjectiveDefinition objective = quest.FindObjective(objectiveId);
                if (objective == null || objective.Optional || objective.GroupIndex != 0 ||
                    objective.EventType != EventType || objective.RequiredCount != RequiredCount || objective.TargetId != null)
                    reason = "Mission objective type/count/group must match the authored mission rules.";
            }

            if (reason != null) return false;
            if (kind == GeneralMissionKind.Rescue)
            {
                if (string.IsNullOrWhiteSpace(rescueTargetId) || string.IsNullOrWhiteSpace(exitId) || rescueMaxHp < 1)
                    reason = "Rescue requires stable target/exit IDs and positive HP.";
            }
            else
            {
                HashSet<string> unique = new(StringComparer.Ordinal);
                if (targetIds == null || targetIds.Length == 0)
                    reason = "Author the target ID roster before accepting this mission.";
                else foreach (string id in targetIds)
                    if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !unique.Add(id))
                        reason = "Target IDs must be unique, non-empty, and trimmed.";
                if (kind == GeneralMissionKind.Suppressors && unique.Count < 3)
                    reason = "Suppressor missions require at least three candidate locations.";
            }
            return reason == null;
        }
    }
}
