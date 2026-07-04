using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestCompletionFlow : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private bool enterRewardStateOnCompletion;

        private void Awake()
        {
            ResolveRuntime();
        }

        private void OnEnable()
        {
            ResolveRuntime();

            if (questRuntime != null)
                questRuntime.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            if (questRuntime != null)
                questRuntime.OnQuestCompleted -= HandleQuestCompleted;
        }

        public void CompleteQuest(string questId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteQuest(questId);

            if (enterRewardStateOnCompletion && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);
        }

        private void HandleQuestCompleted(string questId)
        {
            Debug.Log($"[QuestCompletionFlow] Quest completed. questId={questId}", this);

            if (enterRewardStateOnCompletion && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);
        }

        private void ResolveRuntime()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }
    }
}
