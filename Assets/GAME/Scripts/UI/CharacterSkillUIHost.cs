using Game.Combat.Core;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Scene integration only; runtime data, commands and navigation retain their existing owners.</summary>
    public sealed class CharacterSkillUIHost : MonoBehaviour
    {
        [SerializeField] private UIScreenRouter router;
        [SerializeField] private CharacterSkillPanelView view;
        [SerializeField] private Button openButton;
        [SerializeField] private CombatEntryPoint entryPoint;
        private CharacterSkillPresenter _presenter;
        private GameStateMachine _state;
        internal bool HasPresenter => _presenter != null;

        private void OnEnable()
        {
            view.Shown += HandleShown;
            view.Hidden += HandleHidden;
            view.CloseRequested += HandleClose;
            RuntimeBootstrapper.OnInitialStateApplied += HandleBootstrap;
            BindState();
            if (view.isActiveAndEnabled) HandleShown();
        }

        private void Start()
        {
            BindState();
            if (view.isActiveAndEnabled && _presenter == null) HandleShown();
        }

        private void OnDisable()
        {
            view.Shown -= HandleShown;
            view.Hidden -= HandleHidden;
            view.CloseRequested -= HandleClose;
            RuntimeBootstrapper.OnInitialStateApplied -= HandleBootstrap;
            if (_state != null) _state.OnStateChanged -= HandleState;
            _state = null;
            DisposePresenter();
        }

        private void HandleBootstrap(Scene scene)
        {
            BindState();
            if (view.isActiveAndEnabled) { DisposePresenter(); HandleShown(); }
        }

        private void BindState()
        {
            if (_state != GameStateMachine.Instance)
            {
                if (_state != null) _state.OnStateChanged -= HandleState;
                _state = GameStateMachine.Instance;
                if (_state != null) _state.OnStateChanged += HandleState;
            }
            UpdateEntry();
        }

        private void HandleState(GameState previous, GameState next) => UpdateEntry();
        private void UpdateEntry() => openButton.interactable = router.CanOpenCharacterSkills &&
            entryPoint != null && entryPoint.ActiveSession == null && entryPoint.ActiveStateMachine == null;

        private void HandleShown()
        {
            if (_presenter != null) return;
            CharacterSkillRuntime runtime = CharacterSkillSaveParticipant.Instance?.Runtime;
            PartyRuntime party = PartyRuntime.Instance;
            if (runtime == null || party == null || entryPoint == null || GameStateMachine.Instance == null)
            {
                view.ShowUnavailable();
                return;
            }
            FilmEquipApplication application = new(runtime, party, entryPoint.RegisteredSkillDefinitions, GameStateMachine.Instance, entryPoint);
            _presenter = new CharacterSkillPresenter(view, application, runtime, party, CharacterProgressionService.Instance,
                GameStateMachine.Instance, entryPoint.RegisteredSkillDefinitions);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.CloseButton.gameObject);
        }

        private void HandleHidden()
        {
            DisposePresenter();
            if (EventSystem.current != null && openButton.isActiveAndEnabled)
                EventSystem.current.SetSelectedGameObject(openButton.gameObject);
        }

        private void HandleClose() => router.CloseCharacterSkills();
        private void DisposePresenter() { _presenter?.Dispose(); _presenter = null; }
    }
}
