using System;
using Game.Quest;

namespace Game.Office
{
    [Serializable]
    public sealed class MissionSelectResult
    {
        public bool success;
        public string missionId;
        public string title;
        public string description;
        public string targetFieldSceneName;
        public string targetSpawnPointId;
        public QuestDefinitionSO questDefinition;

        public static MissionSelectResult FromEntry(MissionEntry entry)
        {
            if (entry == null)
                return new MissionSelectResult();

            return new MissionSelectResult
            {
                success = true,
                missionId = entry.MissionId,
                title = entry.Title,
                description = entry.Description,
                targetFieldSceneName = entry.TargetFieldSceneName,
                targetSpawnPointId = entry.TargetSpawnPointId,
                questDefinition = entry.QuestDefinition
            };
        }
    }
}
