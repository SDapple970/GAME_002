using Game.Quest;
using UnityEngine;

namespace Game.Story
{
    [CreateAssetMenu(menuName = "GAME/Story/Case File", fileName = "CaseFile")]
    public sealed class CaseFileDataSO : ScriptableObject
    {
        [SerializeField] private ChapterId chapterId = ChapterId.Chapter_01_Intro;
        [SerializeField] private string caseTitle;
        [TextArea(3, 8)]
        [SerializeField] private string description;
        [SerializeField] private string targetSceneName;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private string clientName;
        [SerializeField] private string locationName;
        [SerializeField] private string rewardPreview;
        [SerializeField] private string objectivePreview;
        [SerializeField] private bool unlocked = true;
        [SerializeField] private QuestDataSO startQuest;

        public ChapterId ChapterId => chapterId;
        public string CaseTitle => caseTitle;
        public string Description => description;
        public string TargetSceneName => targetSceneName;
        public string TargetSpawnPointId => targetSpawnPointId;
        public string ClientName => clientName;
        public string LocationName => locationName;
        public string RewardPreview => rewardPreview;
        public string ObjectivePreview => objectivePreview;
        public bool Unlocked => unlocked;
        public QuestDataSO StartQuest => startQuest;
    }
}
