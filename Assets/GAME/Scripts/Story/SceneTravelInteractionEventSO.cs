using Game.Interaction;
using Game.Quest;
using UnityEngine;

namespace Game.Story
{
    [CreateAssetMenu(menuName = "GAME/Story/Scene Travel Interaction Event", fileName = "SceneTravelInteractionEvent")]
    public sealed class SceneTravelInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string targetSceneName;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private bool requireActiveQuest;
        [SerializeField] private QuestId requiredQuest = QuestId.None;
        [SerializeField] private int minimumStep;

        public override void Execute(InteractionContext context)
        {
            if (!MeetsQuestRequirements(context))
                return;

            SceneTravelService.TravelTo(targetSceneName, targetSpawnPointId);
        }

        private bool MeetsQuestRequirements(InteractionContext context)
        {
            if (!requireActiveQuest)
                return true;

            QuestManager manager = QuestManager.Instance != null
                ? QuestManager.Instance
                : Object.FindFirstObjectByType<QuestManager>();

            if (manager == null)
            {
                Debug.LogWarning("[SceneTravelInteractionEventSO] QuestManager is missing.", context.Target);
                context.Controller?.ShowTemporaryMessage("You cannot enter yet.", 1.5f);
                return false;
            }

            QuestProgress progress = requiredQuest == QuestId.None
                ? manager.GetActiveQuest()
                : manager.GetProgress(requiredQuest);

            if (progress == null || progress.completed)
            {
                Debug.LogWarning($"[SceneTravelInteractionEventSO] Required quest is not active. quest={requiredQuest}", context.Target);
                context.Controller?.ShowTemporaryMessage("You cannot enter yet.", 1.5f);
                return false;
            }

            if (requiredQuest != QuestId.None && progress.QuestId != requiredQuest)
            {
                Debug.LogWarning($"[SceneTravelInteractionEventSO] Quest mismatch. required={requiredQuest}, current={progress.QuestId}", context.Target);
                context.Controller?.ShowTemporaryMessage("You cannot enter yet.", 1.5f);
                return false;
            }

            if (progress.currentStep < minimumStep)
            {
                Debug.LogWarning($"[SceneTravelInteractionEventSO] Quest step is too low. requiredStep={minimumStep}, currentStep={progress.currentStep}", context.Target);
                context.Controller?.ShowTemporaryMessage("You cannot enter yet.", 1.5f);
                return false;
            }

            return true;
        }
    }
}
