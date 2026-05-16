using UnityEngine;

namespace Game.NonCombat.Chapter
{
    [CreateAssetMenu(menuName = "GAME/NonCombat/Chapter Definition", fileName = "ChapterDefinition")]
    public sealed class ChapterDefinitionSO : ScriptableObject
    {
        [SerializeField] private string chapterId;
        [SerializeField] private string displayName;
        [SerializeField] private string startFlag;
        [SerializeField] private string completeFlag;

        public string ChapterId => chapterId;
        public string DisplayName => displayName;
        public string StartFlag => startFlag;
        public string CompleteFlag => completeFlag;
    }
}
