using System;
using Game.Quest;
using Game.Story;
using UnityEngine;

namespace Game.Office
{
    [Serializable]
    public sealed class MissionEntry
    {
        [SerializeField] private string missionId;
        [SerializeField] private string title;
        [TextArea(2, 6)]
        [SerializeField] private string description;
        [SerializeField] private string targetFieldSceneName;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private bool unlocked = true;
        [SerializeField] private QuestDefinitionSO questDefinition;
        [SerializeField] private CaseFileDataSO caseFile;

        public string MissionId => ResolveMissionId();
        public string Title => ResolveTitle();
        public string Description => ResolveDescription();
        public string TargetFieldSceneName => ResolveTargetFieldSceneName();
        public string TargetSpawnPointId => ResolveTargetSpawnPointId();
        public bool Unlocked => unlocked && (caseFile == null || caseFile.Unlocked);
        public QuestDefinitionSO QuestDefinition => questDefinition;
        public CaseFileDataSO CaseFile => caseFile;

        private string ResolveMissionId()
        {
            if (!string.IsNullOrWhiteSpace(missionId))
                return missionId;

            if (questDefinition != null)
            {
                if (!string.IsNullOrWhiteSpace(questDefinition.QuestId))
                    return questDefinition.QuestId;

                return questDefinition.name;
            }

            return caseFile != null ? caseFile.name : string.Empty;
        }

        private string ResolveTitle()
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title;

            if (questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.QuestTitle))
                return questDefinition.QuestTitle;

            if (caseFile != null && !string.IsNullOrWhiteSpace(caseFile.CaseTitle))
                return caseFile.CaseTitle;

            return MissionId;
        }

        private string ResolveDescription()
        {
            if (!string.IsNullOrWhiteSpace(description))
                return description;

            if (questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.Description))
                return questDefinition.Description;

            return caseFile != null ? caseFile.Description : string.Empty;
        }

        private string ResolveTargetFieldSceneName()
        {
            if (!string.IsNullOrWhiteSpace(targetFieldSceneName))
                return targetFieldSceneName;

            return caseFile != null ? caseFile.TargetSceneName : string.Empty;
        }

        private string ResolveTargetSpawnPointId()
        {
            if (!string.IsNullOrWhiteSpace(targetSpawnPointId))
                return targetSpawnPointId;

            return caseFile != null ? caseFile.TargetSpawnPointId : string.Empty;
        }
    }
}
