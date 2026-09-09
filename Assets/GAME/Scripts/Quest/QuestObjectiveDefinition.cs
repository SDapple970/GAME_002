using System;
using UnityEngine;

namespace Game.Quest
{
    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private QuestEventType eventType = QuestEventType.Interact;
        [SerializeField] private string targetId;
        [SerializeField] private int requiredCount = 1;
        [SerializeField] private bool optional;
        [Min(0)]
        [SerializeField] private int groupIndex;
        [SerializeField] private QuestObjectiveVisibility visibility = QuestObjectiveVisibility.Visible;
        [TextArea(2, 4)]
        [SerializeField] private string description;

        public string ObjectiveId => objectiveId;
        public QuestEventType EventType => eventType;
        public string TargetId => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public bool Optional => optional;
        public int GroupIndex => Mathf.Max(0, groupIndex);
        public QuestObjectiveVisibility Visibility => visibility;
        public string Description => description;

        public bool Matches(QuestEvent questEvent)
        {
            if (questEvent.Type != eventType)
                return false;

            if (!string.IsNullOrEmpty(objectiveId) && objectiveId != questEvent.ObjectiveId)
                return false;

            return TargetId == null || TargetId == questEvent.TargetId;
        }
    }
}
