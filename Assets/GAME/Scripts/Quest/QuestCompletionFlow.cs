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
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }

        public void CompleteQuest(string questId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteQuest(questId);

            if (enterRewardStateOnCompletion && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);
        }
    }
}
