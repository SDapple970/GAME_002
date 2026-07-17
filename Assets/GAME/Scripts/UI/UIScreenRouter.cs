using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIScreenRouter : MonoBehaviour
    {
        [SerializeField] private GameUIRootController uiRoot;
        [SerializeField] private GameStateMachine stateMachine;
        [SerializeField] private bool logRouteChanges;

        private GameStateMachine _subscribedStateMachine;
        private bool _missingUiRootWarned;
        private bool _missingStateMachineWarned;
        private GameState? _lastState;
        private GameState? _lastContentState;

        internal GameState? CurrentRoutedState => _lastState;
        internal GameState? CurrentContentState => _lastContentState;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveAndSubscribe();
            ApplyCurrentRoute();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeFromStateMachine();
        }

        public void ApplyCurrentRoute()
        {
            ResolveReferences();
            Apply(stateMachine != null ? stateMachine.Current : GameState.Exploration);
        }

        internal void ApplyState(GameState state)
        {
            Apply(state);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveAndSubscribe();
            ApplyCurrentRoute();
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

            GameState contentState = state == GameState.Paused
                ? ResolvePausedContentState()
                : state;

            ApplyContentRoute(contentState);
            uiRoot.SetPauseVisible(state == GameState.Paused);

            bool changed = _lastState != state || _lastContentState != contentState;
            _lastState = state;
            _lastContentState = contentState;

            if (changed && logRouteChanges)
                Debug.Log($"[UIScreenRouter] State={state}, Content={contentState}, Route={DescribeRoute(contentState)}", this);
        }

        private void ApplyContentRoute(GameState state)
        {
            bool showDialogue = state == GameState.Dialogue ||
                                state == GameState.Choice ||
                                state == GameState.Cutscene;

            // Dungeon 1 presents narrative UI over the field. FieldRoot is the field HUD,
            // never the field world/camera, so the world remains outside this router.
            bool showField = state == GameState.Exploration ||
                             state == GameState.Dialogue ||
                             state == GameState.Choice;

            uiRoot.SetTitleVisible(state == GameState.Title);
            uiRoot.SetFieldVisible(showField);
            uiRoot.SetDialogueVisible(showDialogue);
            uiRoot.SetChoiceVisible(state == GameState.Choice);
            uiRoot.SetCombatVisible(GameStateMachine.IsCombatState(state));
            uiRoot.SetRewardVisible(state == GameState.Reward);
            uiRoot.SetLoadingVisible(state == GameState.Loading || state == GameState.Boot);
        }

        private GameState ResolvePausedContentState()
        {
            if (stateMachine == null)
                return _lastContentState ?? GameState.Exploration;

            GameState previous = stateMachine.Previous;
            return previous == GameState.Paused
                ? _lastContentState ?? GameState.Exploration
                : previous;
        }

        private void ResolveAndSubscribe()
        {
            ResolveReferences();

            if (_subscribedStateMachine == stateMachine)
                return;

            UnsubscribeFromStateMachine();
            _subscribedStateMachine = stateMachine;
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged += HandleStateChanged;
        }

        private void UnsubscribeFromStateMachine()
        {
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleStateChanged;

            _subscribedStateMachine = null;
        }

        private void ResolveReferences()
        {
            if (uiRoot == null)
                uiRoot = FindFirstObjectByType<GameUIRootController>(FindObjectsInactive.Include);

            GameStateMachine singleton = GameStateMachine.Instance;
            if (singleton != null && stateMachine != singleton)
                stateMachine = singleton;
            else if (stateMachine == null)
                stateMachine = FindFirstObjectByType<GameStateMachine>(FindObjectsInactive.Include);

            WarnIfMissingStateMachine();
            WarnIfMissingUiRoot();
        }

        private static string DescribeRoute(GameState state)
        {
            if (GameStateMachine.IsCombatState(state))
                return "Combat";

            return state switch
            {
                GameState.Boot => "Loading",
                GameState.Title => "Title",
                GameState.Exploration => "Field",
                GameState.Dialogue => "Field+Dialogue",
                GameState.Choice => "Field+Dialogue+Choice",
                GameState.Reward => "Reward",
                GameState.Cutscene => "Dialogue",
                GameState.Loading => "Loading",
                GameState.UIOnly => "None (compatibility)",
                _ => "None"
            };
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
