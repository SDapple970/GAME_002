// Assets/GAME/Scripts/Mission/Runtime/Data/MissionDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Mission.Data
{
    [CreateAssetMenu(menuName = "GAME/Mission/Mission Definition")]
    public sealed class MissionDefinitionSO : ScriptableObject
    {
        [SerializeField] private string missionId;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private List<MissionObjective> objectives = new();
        [SerializeField] private bool autoCompleteWhenAllObjectivesComplete = true;

        public string MissionId => missionId;
        public string Title => title;
        public string Description => description;
        public IReadOnlyList<MissionObjective> Objectives => objectives;
        public bool AutoCompleteWhenAllObjectivesComplete => autoCompleteWhenAllObjectivesComplete;

        public MissionObjective GetObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId) || objectives == null) return null;

            foreach (MissionObjective objective in objectives)
            {
                if (objective != null && objective.ObjectiveId == objectiveId)
                {
                    return objective;
                }
            }

            return null;
        }
    }
}
