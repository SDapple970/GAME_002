using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIScreenRouter : MonoBehaviour
    {
        [SerializeField] private GameUIRootController uiRoot;
        [SerializeField] private GameStateMachine stateMachine;

        private bool _missingUiRootWarned;
        private bool _missingStateMachineWarned;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (stateMachine != null)
                stateMachine.OnStateChanged += HandleStateChanged;

            Apply(GameStateMachine.Instance != null ? GameStateMachine.Instance.Current : GameState.Exploration);
        }

        private void OnDisable()
        {
            if (stateMachine != null)
                stateMachine.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            Apply(next);
        }

        private void Apply(GameState state)
        {
            if (uiRoot == null)
            {
                WarnIfMissingUiRoot();
                return;
            }

            bool isCombat = GameStateMachine.IsCombatState(state);

            uiRoot.SetTitleVisible(state == GameState.Title);
            uiRoot.SetFieldVisible(state == GameState.Exploration);
            uiRoot.SetDialogueVisible(state == GameState.Dialogue || state == GameState.Cutscene);
            uiRoot.SetChoiceVisible(state == GameState.Choice);
            uiRoot.SetCombatVisible(isCombat);
            uiRoot.SetRewardVisible(state == GameState.Reward);
            uiRoot.SetPauseVisible(state == GameState.Paused);
            uiRoot.SetLoadingVisible(state == GameState.Loading || state == GameState.Boot);
        }

        private void ResolveReferences()
        {
            if (uiRoot == null)
                uiRoot = FindFirstObjectByType<GameUIRootController>(FindObjectsInactive.Include);

            if (stateMachine == null)
                stateMachine = GameStateMachine.Instance != null
                    ? GameStateMachine.Instance
                    : FindFirstObjectByType<GameStateMachine>();

            WarnIfMissingStateMachine();
            WarnIfMissingUiRoot();
        }

        private void WarnIfMissingUiRoot()
        {
            if (uiRoot != null || _missingUiRootWarned)
                return;

            _missingUiRootWarned = true;
            Debug.LogWarning("[UIScreenRouter] GameUIRootController is missing. GameState changes cannot route root UI visibility.", this);
        }

        private void WarnIfMissingStateMachine()
        {
            if (stateMachine != null || _missingStateMachineWarned)
                return;

            _missingStateMachineWarned = true;
            Debug.LogWarning("[UIScreenRouter] GameStateMachine is missing. UI routing will not receive state changes.", this);
        }
    }
}
