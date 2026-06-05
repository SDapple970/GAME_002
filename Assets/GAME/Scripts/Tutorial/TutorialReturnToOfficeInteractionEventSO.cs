using Game.Interaction;
using Game.Quest;
using Game.Story;
using UnityEngine;

namespace Game.Tutorial
{
    [CreateAssetMenu(menuName = "GAME/Tutorial/Return To Office Interaction Event", fileName = "TutorialReturnToOfficeInteractionEvent")]
    public sealed class TutorialReturnToOfficeInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string officeSceneName;
        [SerializeField] private string officeSpawnPointId;
        [SerializeField] private QuestId requiredQuest = QuestId.TutorialPermit;
        [SerializeField] private bool requireQuestCompleted = true;

        public override void Execute(InteractionContext context)
        {
            if (!CanReturn())
            {
                context.Controller?.ShowTemporaryMessage("아직 돌아갈 수 없다.", 1.5f);
                return;
            }

            SceneTravelService.TravelTo(officeSceneName, officeSpawnPointId);
        }

        private bool CanReturn()
        {
            if (requiredQuest == QuestId.None)
                return true;

            QuestManager manager = QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
            if (manager == null)
                return !requireQuestCompleted;

            QuestProgress progress = manager.GetProgress(requiredQuest);
            if (progress == null)
                return !requireQuestCompleted;

            return requireQuestCompleted ? progress.completed : true;
        }
    }
}
