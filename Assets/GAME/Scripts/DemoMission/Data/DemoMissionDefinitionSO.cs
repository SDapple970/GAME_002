using System.Collections.Generic;
using UnityEngine;

namespace Game.DemoMission.Data
{
    [CreateAssetMenu(menuName = "GAME_002/Demo Mission/Demo Mission Definition")]
    public sealed class DemoMissionDefinitionSO : ScriptableObject
    {
        public string missionId;
        public string missionTitle;
        public string dungeonSceneName;
        public RescueNpcDefinitionSO rescueTarget;
        public List<MonsterBriefingEntry> monsters = new();
        public int requiredEnemyKills = 1;
        [TextArea] public string objectiveDescription;
    }
}
