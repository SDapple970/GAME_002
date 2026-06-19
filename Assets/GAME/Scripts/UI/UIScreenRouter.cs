using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIScreenRouter : MonoBehaviour
    {
        [SerializeField] private GameUIRootController uiRoot;
        [SerializeField] private GameStateMachine stateMachine;

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
                return;

            bool isCombat = GameStateMachine.IsCombatState(state);

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
        }
    }
}
