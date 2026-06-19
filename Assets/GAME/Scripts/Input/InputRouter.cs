using Game.Core;

namespace Game.Input
{
    public sealed class InputRouter
    {
        public bool AllowsExplorationInput()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.AllowsExplorationInput();
        }

        public bool AllowsUIInput()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.AllowsUIInput();
        }

        public bool AllowsPauseInput()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Current != GameState.Loading;
        }

        public bool AllowsDialogueAdvanceInput()
        {
            if (GameStateMachine.Instance == null)
                return true;

            GameState state = GameStateMachine.Instance.Current;
            return state == GameState.Dialogue ||
                   state == GameState.Choice ||
                   state == GameState.Cutscene ||
                   state == GameState.UIOnly;
        }
    }
}
