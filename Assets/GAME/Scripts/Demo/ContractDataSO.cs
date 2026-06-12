using Game.Quest;
using UnityEngine;

namespace Game.Demo
{
    [CreateAssetMenu(menuName = "GAME/Demo/Contract Data", fileName = "ContractData")]
    public sealed class ContractDataSO : ScriptableObject
    {
        [SerializeField] private string contractTitle;
        [TextArea(2, 6)]
        [SerializeField] private string objectiveText;
        [SerializeField] private Sprite monsterPortrait;
        [SerializeField] private Sprite rescueNpcPortrait;
        [TextArea(2, 6)]
        [SerializeField] private string monsterDescription;
        [TextArea(2, 6)]
        [SerializeField] private string rescueNpcDescription;
        [SerializeField] private string targetSceneName;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private QuestDataSO startQuest;

        public string ContractTitle => contractTitle;
        public string ObjectiveText => objectiveText;
        public Sprite MonsterPortrait => monsterPortrait;
        public Sprite RescueNpcPortrait => rescueNpcPortrait;
        public string MonsterDescription => monsterDescription;
        public string RescueNpcDescription => rescueNpcDescription;
        public string TargetSceneName => targetSceneName;
        public string TargetSpawnPointId => targetSpawnPointId;
        public QuestDataSO StartQuest => startQuest;
    }
}
