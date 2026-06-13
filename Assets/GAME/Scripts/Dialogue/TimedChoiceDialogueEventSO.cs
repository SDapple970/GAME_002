using Game.Core;
using Game.Interaction;
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "GAME/Dialogue/Timed Choice Dialogue Event", fileName = "TimedChoiceDialogueEvent")]
    public sealed class TimedChoiceDialogueEventSO : InteractionEventSO
    {
        public string speakerName;
        [TextArea(2, 6)] public string bodyText;
        public TimedChoiceOption optionA;
        public TimedChoiceOption optionB;
        public float timeLimitSeconds = 5f;
        [Range(0, 1)] public int timeoutDefaultOptionIndex;
        public bool autoSelectOnTimeout = true;
        public bool logSelection = true;

        public override void Execute(InteractionContext context)
        {
            if (IsCombatBlockingState())
                return;

            TimedChoiceDialoguePanel panel = TimedChoiceDialoguePanel.Instance;
            if (panel == null)
                panel = Object.FindFirstObjectByType<TimedChoiceDialoguePanel>();

            if (panel == null)
            {
                context.Controller?.ShowTemporaryMessage(bodyText, 2f);
                return;
            }

            panel.Show(this, context);
        }

        public TimedChoiceOption GetOption(int optionIndex)
        {
            return Mathf.Clamp(optionIndex, 0, 1) == 0 ? optionA : optionB;
        }

        public int GetClampedTimeoutDefaultOptionIndex()
        {
            return Mathf.Clamp(timeoutDefaultOptionIndex, 0, 1);
        }

        public void ExecuteOption(int optionIndex, InteractionContext context)
        {
            int clampedIndex = Mathf.Clamp(optionIndex, 0, 1);
            TimedChoiceOption selectedOption = GetOption(clampedIndex);

            if (logSelection)
            {
                string label = GetOptionLabel(selectedOption);
                Debug.Log($"[TimedChoiceDialogue] Selected option {clampedIndex}: {label}", context.Target);
            }

            ExecuteAfterSelectEvents(selectedOption, context);
        }

        public static string GetOptionLabel(TimedChoiceOption option)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.displayText))
                return "(선택)";

            return option.displayText;
        }

        private static void ExecuteAfterSelectEvents(TimedChoiceOption option, InteractionContext context)
        {
            if (option?.afterSelectEvents == null)
                return;

            for (int i = 0; i < option.afterSelectEvents.Length; i++)
            {
                InteractionEventSO interactionEvent = option.afterSelectEvents[i];
                if (interactionEvent == null)
                    continue;

                interactionEvent.Execute(context);
            }
        }

        private void OnValidate()
        {
            timeLimitSeconds = Mathf.Max(0f, timeLimitSeconds);
            timeoutDefaultOptionIndex = Mathf.Clamp(timeoutDefaultOptionIndex, 0, 1);
        }

        private static bool IsCombatBlockingState()
        {
            return GameStateMachine.Instance != null &&
                   (GameStateMachine.Instance.Is(GameState.Combat) ||
                    GameStateMachine.Instance.Is(GameState.CombatTransition));
        }
    }
}
