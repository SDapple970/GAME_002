using Game.Core;

namespace Game.Input
{
    public sealed class InputRouter
    {
        public enum InteractionDestination
        {
            None,
            Exploration,
            DialogueAdvance
        }

        public bool AllowsExplorationInput()
        {
            if (GameStateMachine.Instance == null)
                return true;

            switch (GameStateMachine.Instance.Current)
            {
                case GameState.Exploration:
                    return true;
                case GameState.Boot:
                case GameState.Title:
                case GameState.Loading:
                case GameState.Dialogue:
                case GameState.Choice:
                case GameState.CombatTransition:
                case GameState.CombatPlanning:
                case GameState.CombatResolving:
                case GameState.Reward:
                case GameState.Cutscene:
                case GameState.UIOnly:
                case GameState.Paused:
                default:
                    return false;
            }
        }

        public bool AllowsUIInput()
        {
            if (GameStateMachine.Instance == null)
                return false;

            switch (GameStateMachine.Instance.Current)
            {
                case GameState.Title:
                case GameState.Dialogue:
                case GameState.Choice:
                case GameState.CombatPlanning:
                case GameState.Reward:
                case GameState.Cutscene:
                case GameState.UIOnly:
                case GameState.Paused:
                    return true;
                case GameState.Boot:
                case GameState.Exploration:
                case GameState.Loading:
                case GameState.CombatTransition:
                case GameState.CombatResolving:
                default:
                    return false;
            }
        }

        public bool AllowsPauseInput()
        {
            if (GameStateMachine.Instance == null)
                return true;

            switch (GameStateMachine.Instance.Current)
            {
                case GameState.Title:
                case GameState.Exploration:
                case GameState.Dialogue:
                case GameState.Choice:
                case GameState.CombatTransition:
                case GameState.CombatPlanning:
                case GameState.CombatResolving:
                case GameState.Reward:
                case GameState.Cutscene:
                case GameState.UIOnly:
                case GameState.Paused:
                    return true;
                case GameState.Boot:
                case GameState.Loading:
                default:
                    return false;
            }
        }

        public bool AllowsDialogueAdvanceInput()
        {
            if (GameStateMachine.Instance == null)
                return false;

            switch (GameStateMachine.Instance.Current)
            {
                case GameState.Dialogue:
                case GameState.Cutscene:
                    return true;
                case GameState.Boot:
                case GameState.Title:
                case GameState.Exploration:
                case GameState.Loading:
                case GameState.Choice:
                case GameState.CombatTransition:
                case GameState.CombatPlanning:
                case GameState.CombatResolving:
                case GameState.Reward:
                case GameState.UIOnly:
                case GameState.Paused:
                default:
                    return false;
            }
        }

        public InteractionDestination ResolveInteractionDestination()
        {
            if (GameStateMachine.Instance == null)
                return InteractionDestination.Exploration;

            switch (GameStateMachine.Instance.Current)
            {
                case GameState.Exploration:
                    return InteractionDestination.Exploration;
                case GameState.Dialogue:
                case GameState.Cutscene:
                    return InteractionDestination.DialogueAdvance;
                case GameState.Boot:
                case GameState.Title:
                case GameState.Loading:
                case GameState.Choice:
                case GameState.CombatTransition:
                case GameState.CombatPlanning:
                case GameState.CombatResolving:
                case GameState.Reward:
                case GameState.UIOnly:
                case GameState.Paused:
                default:
                    return InteractionDestination.None;
            }
        }
    }
}
