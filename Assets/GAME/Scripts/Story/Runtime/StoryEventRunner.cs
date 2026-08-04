using System;
using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.Input;
using Game.Story.Data;
using Game.Story.UI;
using UnityEngine;

namespace Game.Story
{
    public sealed class StoryEventRunner : MonoBehaviour
    {
        private enum StoryRunLifecycle
        {
            Idle,
            Starting,
            ShowingDialogue,
            WaitingForChoice,
            Ending,
            Completed,
            Cancelled
        }

        private enum PresenterKind
        {
            None,
            StoryDialogueHud,
            DialoguePanel
        }

        private static StoryEventRunner _activeRunner;
        private static int _nextGeneration;

        [SerializeField] private DialoguePanel dialoguePanel;
        [SerializeField] private StoryDialogueHUD storyDialogueHUD;
        [SerializeField] private TimedChoiceDialoguePanel timedChoiceDialoguePanel;
        [SerializeField] private GameState stateWhileRunning = GameState.UIOnly;
        [SerializeField] private bool restoreExplorationOnEnd = true;
        [SerializeField] private bool markCurrentEventCompletedOnEnd = true;

        private StoryEventDefinitionSO _currentEvent;
        private StoryNode _currentNode;
        private readonly List<ResolvedStoryChoice> _resolvedChoices = new();
        private StorySpeakerAnchor _currentSpeakerAnchor;
        private StoryRunLifecycle _lifecycle;
        private PresenterKind _presenter;
        private GameState _returnState;
        private GameState _ownedNarrativeState;
        private bool _ownsNarrativeState;
        private bool _completionRaised;
        private int _generation;
        private int _nodeToken;
        private int _lastAdvanceFrame = -1;
        private float _choiceTimeoutAt;
        private bool _choiceTimerRunning;
        private GameInputInstaller _inputInstaller;
        private InputService _inputService;
        private bool _inputSubscribed;
        private bool _ambiguousHudWarned;
        private bool _ambiguousPanelWarned;
        private bool _missingFlowWarned;

        public bool IsRunning =>
            _lifecycle == StoryRunLifecycle.Starting ||
            _lifecycle == StoryRunLifecycle.ShowingDialogue ||
            _lifecycle == StoryRunLifecycle.WaitingForChoice ||
            _lifecycle == StoryRunLifecycle.Ending;

        public IReadOnlyList<ResolvedStoryChoice> CurrentResolvedChoices => _resolvedChoices;

        public event Action<StoryEventDefinitionSO> OnEventStarted;
        public event Action<StoryEventDefinitionSO> OnEventCompleted;

        private void Awake()
        {
            _lifecycle = StoryRunLifecycle.Idle;
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureInputSubscription();
        }

        private void OnDisable()
        {
            CancelActiveRun("runner disabled");
            UnsubscribeInput();
        }

        private void OnDestroy()
        {
            CancelActiveRun("runner destroyed");
            if (_activeRunner == this)
                _activeRunner = null;
        }

        private void Update()
        {
            // Late bootstrap recovery uses only the persistent installer reference.
            EnsureInputSubscription();
            if (_choiceTimerRunning &&
                _lifecycle == StoryRunLifecycle.WaitingForChoice &&
                Time.unscaledTime >= _choiceTimeoutAt)
            {
                _choiceTimerRunning = false;
                TryAcceptTimeout(_generation, _nodeToken);
            }
        }

        public void StartEvent(StoryEventDefinitionSO eventDefinition)
        {
            TryStartEvent(eventDefinition, null);
        }

        public void StartEvent(StoryEventDefinitionSO eventDefinition, StorySpeakerAnchor speakerAnchor)
        {
            TryStartEvent(eventDefinition, speakerAnchor);
        }

        public bool TryStartEvent(StoryEventDefinitionSO eventDefinition)
        {
            return TryStartEvent(eventDefinition, null);
        }

        public bool TryStartEvent(StoryEventDefinitionSO eventDefinition, StorySpeakerAnchor speakerAnchor)
        {
            return TryStartEventInternal(eventDefinition, speakerAnchor);
        }

        public void Advance()
        {
            if (_lifecycle != StoryRunLifecycle.ShowingDialogue || _currentNode == null)
                return;

            if (!IsNarrativeAdvanceState())
                return;

            int frame = Time.frameCount;
            if (_lastAdvanceFrame == frame)
                return;
            _lastAdvanceFrame = frame;

            StoryNode sourceNode = _currentNode;
            int sourceToken = _nodeToken;
            if (sourceNode.EndEvent || string.IsNullOrEmpty(sourceNode.NextNodeId))
            {
                EndEvent();
                return;
            }

            StoryNode nextNode = _currentEvent?.GetNode(sourceNode.NextNodeId);
            if (!IsCurrentNode(sourceToken) || nextNode == null)
            {
                if (nextNode == null)
                {
                    Debug.LogWarning(
                        $"[StoryEventRunner] Next node not found. event='{_currentEvent?.EventId}' from='{sourceNode.NodeId}' next='{sourceNode.NextNodeId}'.",
                        this);
                    EndEvent();
                }
                return;
            }

            ShowNode(nextNode);
        }

        public void SelectChoice(StoryChoice choice)
        {
            TryAcceptChoice(choice, _generation, _nodeToken);
        }

        public void SelectChoiceByIndex(int visibleIndex)
        {
            if (_lifecycle != StoryRunLifecycle.WaitingForChoice ||
                !IsChoiceState() ||
                visibleIndex < 0 || visibleIndex >= _resolvedChoices.Count)
            {
                return;
            }

            TryAcceptResolvedChoice(_resolvedChoices[visibleIndex], _generation, _nodeToken, false);
        }

        public void EndEvent()
        {
            if (!IsRunning || _lifecycle == StoryRunLifecycle.Ending)
                return;

            _lifecycle = StoryRunLifecycle.Ending;
            StoryEventDefinitionSO completedEvent = _currentEvent;
            MarkCurrentEventCompletedIfNeeded();
            HideOwnedPresenter();
            ClearRunData();
            _lifecycle = StoryRunLifecycle.Completed;
            ReleaseActiveOwnership();

            if (!_completionRaised)
            {
                _completionRaised = true;
                OnEventCompleted?.Invoke(completedEvent);
            }

            RestoreOwnedStateIfSafe();
        }

        private bool TryStartEventInternal(StoryEventDefinitionSO eventDefinition, StorySpeakerAnchor speakerAnchor)
        {
            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryEventRunner] Cannot start a null story event.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(eventDefinition.EventId))
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Cannot start Story asset='{eventDefinition.name}' because eventId is empty.",
                    this);
                return false;
            }

            if (IsRunning)
            {
                Debug.LogWarning($"[StoryEventRunner] Event already running. Ignored event='{eventDefinition.EventId}'.", this);
                return false;
            }

            PruneActiveRunner();
            if (_activeRunner != null && _activeRunner != this)
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Duplicate Production runner blocked. Active='{_activeRunner.name}', Duplicate='{name}', event='{eventDefinition.EventId}'.",
                    this);
                return false;
            }

            GameStateMachine stateMachine = GameStateMachine.Instance;
            GameFlowController flow = GameFlowController.Instance;
            if (stateMachine == null || flow == null)
            {
                WarnMissingFlow(eventDefinition);
                return false;
            }

            bool cutsceneAuthored = stateWhileRunning == GameState.Cutscene;
            GameState sourceState = stateMachine.Current;
            if ((!cutsceneAuthored && sourceState != GameState.Exploration) ||
                (cutsceneAuthored && sourceState != GameState.Exploration && sourceState != GameState.Cutscene))
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Story start rejected from state={sourceState}. event='{eventDefinition.EventId}'.",
                    this);
                return false;
            }

            _activeRunner = this;
            _generation = ++_nextGeneration;
            _nodeToken = 0;
            _lastAdvanceFrame = -1;
            _completionRaised = false;
            _currentEvent = eventDefinition;
            _currentSpeakerAnchor = speakerAnchor;
            _returnState = sourceState == GameState.Cutscene ? GameState.Cutscene : GameState.Exploration;
            _lifecycle = StoryRunLifecycle.Starting;
            ResolvePresenter();

            if (cutsceneAuthored && sourceState == GameState.Exploration &&
                !RequestNarrativeState(GameState.Cutscene, "StartCutsceneStory"))
            {
                CancelActiveRun("cutscene state request rejected");
                return false;
            }

            StoryNode firstNode = eventDefinition.GetNode(eventDefinition.StartNodeId);
            if (firstNode == null)
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Start node not found. event='{eventDefinition.EventId}' node='{eventDefinition.StartNodeId}'.",
                    this);
                CancelActiveRun("start node missing");
                return false;
            }

            OnEventStarted?.Invoke(eventDefinition);
            ShowNode(firstNode);
            return IsRunning;
        }

        private void ShowNode(StoryNode node)
        {
            if (!IsRunning || node == null)
                return;

            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Story node has an empty nodeId. event='{_currentEvent?.EventId}'.",
                    this);
                CancelActiveRun("node id missing");
                return;
            }

            if (!RequestNarrativeState(GameState.Dialogue, "ShowStoryNode"))
            {
                CancelActiveRun("dialogue state request rejected");
                return;
            }

            _currentNode = node;
            int nodeToken = ++_nodeToken;
            _lifecycle = StoryRunLifecycle.ShowingDialogue;
            ApplyNodeEffects(node, nodeToken);
            if (!IsCurrentNode(nodeToken))
                return;

            ShowLine(node);
            bool hasChoices = node.Choices != null && node.Choices.Count > 0;
            if (!hasChoices)
            {
                HideChoices();
                if (_presenter == PresenterKind.DialoguePanel)
                    dialoguePanel?.SetNextVisible(true);
                return;
            }

            ShowChoices(node, nodeToken);
        }

        private void ShowLine(StoryNode node)
        {
            if (_presenter == PresenterKind.StoryDialogueHud)
            {
                storyDialogueHUD.ShowLine(_currentSpeakerAnchor, node);
                return;
            }

            if (_presenter == PresenterKind.DialoguePanel)
            {
                BindDialoguePanelAdvance();
                dialoguePanel.Show();
                dialoguePanel.SetLine(node.SpeakerName, node.Body, node.Portrait);
                dialoguePanel.ClearChoices();
                return;
            }

            Debug.LogWarning(
                $"[StoryEventRunner] No valid dialogue presenter. event='{_currentEvent?.EventId}' node='{node.NodeId}'.",
                this);
        }

        private void ShowChoices(StoryNode node, int nodeToken)
        {
            _resolvedChoices.Clear();
            _resolvedChoices.AddRange(StoryChoiceResolver.Resolve(node.Choices, this));
            bool hasSelectableChoice = HasSelectableChoice(_resolvedChoices);
            bool hasValidTimerRoute = HasValidTimerRoute(node, _resolvedChoices);
            if (!hasSelectableChoice && !hasValidTimerRoute)
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] No selectable choice or valid timeout route. Continuing deterministically. event='{_currentEvent?.EventId}' node='{node.NodeId}'.",
                    this);
                ContinueWithoutChoice(node);
                return;
            }

            if (!RequestNarrativeState(GameState.Choice, "ShowStoryChoices"))
            {
                CancelActiveRun("choice state request rejected");
                return;
            }

            _lifecycle = StoryRunLifecycle.WaitingForChoice;
            _choiceTimerRunning = node.UseTimedChoices && node.ChoiceTimeLimitSeconds > 0f;
            _choiceTimeoutAt = _choiceTimerRunning
                ? Time.unscaledTime + node.ChoiceTimeLimitSeconds
                : 0f;
            int generation = _generation;
            if (_presenter == PresenterKind.StoryDialogueHud && storyDialogueHUD.CanPresentChoices)
            {
                storyDialogueHUD.ShowTimedChoices(
                    node,
                    _resolvedChoices,
                    choice => TryAcceptResolvedChoice(choice, generation, nodeToken, false),
                    () => TryAcceptTimeout(generation, nodeToken));
                return;
            }

            if (_presenter == PresenterKind.DialoguePanel)
            {
                dialoguePanel.SetNextVisible(false);
                dialoguePanel.BuildChoices(
                    _resolvedChoices,
                    choice => TryAcceptResolvedChoice(choice, generation, nodeToken, false));
                return;
            }

            Debug.LogWarning(
                $"[StoryEventRunner] No active choice presenter. Waiting for an explicit routed selection command. event='{_currentEvent?.EventId}' node='{node.NodeId}'.",
                this);
        }

        private void TryAcceptChoice(StoryChoice choice, int generation, int nodeToken)
        {
            if (!TryFindResolvedChoice(choice, out ResolvedStoryChoice resolved))
                return;

            TryAcceptResolvedChoice(resolved, generation, nodeToken, false);
        }

        private void TryAcceptResolvedChoice(
            ResolvedStoryChoice resolved,
            int generation,
            int nodeToken,
            bool isTimeoutSelection)
        {
            StoryChoice choice = resolved.Choice;
            if (_lifecycle != StoryRunLifecycle.WaitingForChoice ||
                !IsChoiceState() || generation != _generation || !IsCurrentNode(nodeToken) || choice == null ||
                !resolved.IsEnabled || !ContainsCurrentChoice(choice) || !choice.AreConditionsMet())
                return;

            // Claim the outcome before effects so a reentrant click/timeout cannot win.
            _lifecycle = StoryRunLifecycle.ShowingDialogue;
            _choiceTimerRunning = false;
            HideChoices();
            choice.ApplyEffects(new StoryEffectContext(
                gameObject,
                StoryOutcomeKind.Choice,
                _currentEvent?.EventId,
                _currentNode?.NodeId,
                resolved.ChoiceId,
                resolved.AuthoredIndex,
                isTimeoutSelection: isTimeoutSelection));
            if (!IsCurrentNode(nodeToken) || !IsRunning)
                return;

            ContinueAfterChoice(choice.NextNodeId);
        }

        private void TryAcceptTimeout(int generation, int nodeToken)
        {
            if (_lifecycle != StoryRunLifecycle.WaitingForChoice ||
                !IsChoiceState() || generation != _generation || !IsCurrentNode(nodeToken))
            {
                return;
            }

            StoryNode timeoutSource = _currentNode;
            _lifecycle = StoryRunLifecycle.ShowingDialogue;
            _choiceTimerRunning = false;
            HideChoices();

            if (!string.IsNullOrEmpty(timeoutSource.TimeoutNodeId))
            {
                StoryNode timeoutNode = _currentEvent?.GetNode(timeoutSource.TimeoutNodeId);
                if (timeoutNode != null)
                {
                    ShowNode(timeoutNode);
                    return;
                }

                Debug.LogWarning(
                    $"[StoryEventRunner] Timeout node not found. event='{_currentEvent?.EventId}' node='{timeoutSource.NodeId}' timeout='{timeoutSource.TimeoutNodeId}'.",
                    this);
            }

            int choiceIndex = timeoutSource.TimeoutChoiceIndex;
            if (choiceIndex >= 0 && choiceIndex < _resolvedChoices.Count)
            {
                ResolvedStoryChoice resolved = _resolvedChoices[choiceIndex];
                if (resolved.IsEnabled && resolved.Choice != null && resolved.Choice.AreConditionsMet())
                {
                    // Restore the waiting lifecycle so selection uses the same single-winner path.
                    _lifecycle = StoryRunLifecycle.WaitingForChoice;
                    TryAcceptResolvedChoice(resolved, generation, nodeToken, true);
                    return;
                }
            }

            Debug.LogWarning(
                $"[StoryEventRunner] Timeout target is missing or disabled. Continuing deterministically. event='{_currentEvent?.EventId}' node='{timeoutSource.NodeId}' index={choiceIndex}.",
                this);
            ContinueWithoutChoice(timeoutSource);
        }

        private void ContinueAfterChoice(string nextNodeId)
        {
            if (string.IsNullOrEmpty(nextNodeId))
            {
                EndEvent();
                return;
            }

            StoryNode nextNode = _currentEvent?.GetNode(nextNodeId);
            if (nextNode == null)
            {
                Debug.LogWarning(
                    $"[StoryEventRunner] Choice next node not found. event='{_currentEvent?.EventId}' next='{nextNodeId}'.",
                    this);
                EndEvent();
                return;
            }

            ShowNode(nextNode);
        }

        private void ApplyNodeEffects(StoryNode node, int nodeToken)
        {
            if (node.Effects == null)
                return;

            for (int i = 0; i < node.Effects.Count; i++)
            {
                StoryEffect effect = node.Effects[i];
                effect?.Apply(new StoryEffectContext(
                    gameObject,
                    StoryOutcomeKind.Story,
                    _currentEvent?.EventId,
                    node.NodeId,
                    effectIndex: i));
            }
        }

        private bool RequestNarrativeState(GameState state, string reason)
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            GameFlowController flow = GameFlowController.Instance;
            if (stateMachine == null || flow == null)
            {
                WarnMissingFlow(_currentEvent);
                return false;
            }

            if (stateMachine.Current == state)
            {
                _ownedNarrativeState = state;
                _ownsNarrativeState = true;
                return true;
            }

            if (!flow.RequestState(state, $"{nameof(StoryEventRunner)}.{reason}"))
                return false;

            _ownedNarrativeState = state;
            _ownsNarrativeState = true;
            return true;
        }

        private void RestoreOwnedStateIfSafe()
        {
            if (!restoreExplorationOnEnd || !_ownsNarrativeState)
                return;

            GameStateMachine stateMachine = GameStateMachine.Instance;
            GameFlowController flow = GameFlowController.Instance;
            if (stateMachine == null || flow == null)
                return;

            GameState current = stateMachine.Current;
            if (current != _ownedNarrativeState ||
                (current != GameState.Dialogue && current != GameState.Choice && current != GameState.Cutscene))
            {
                return;
            }

            flow.RequestState(_returnState, $"{nameof(StoryEventRunner)}.RestoreOwnedState");
            _ownsNarrativeState = false;
        }

        private void CancelActiveRun(string reason)
        {
            if (!IsRunning)
                return;

            HideOwnedPresenter();
            ClearRunData();
            _lifecycle = StoryRunLifecycle.Cancelled;
            ReleaseActiveOwnership();
            RestoreOwnedStateIfSafe();
            Debug.LogWarning($"[StoryEventRunner] Active story run cancelled safely: {reason}.", this);
        }

        private void MarkCurrentEventCompletedIfNeeded()
        {
            if (!markCurrentEventCompletedOnEnd || _currentEvent == null)
                return;

            string eventId = _currentEvent.EventId;
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning($"[StoryEventRunner] Event id is empty. Completion was not marked for event asset='{_currentEvent.name}'.", this);
                return;
            }

            if (StoryProgressManager.Instance == null)
            {
                Debug.LogWarning($"[StoryEventRunner] StoryProgressManager missing. Completion was not marked for event='{eventId}'.", this);
                return;
            }

            StoryProgressManager.Instance.MarkEventCompleted(eventId);
        }

        private void ResolveReferences()
        {
            _ = timedChoiceDialoguePanel; // Serialized compatibility panel; it owns only its authored local flow.
        }

        private void ResolvePresenter()
        {
            _presenter = PresenterKind.None;
            StoryDialogueHUD preferredHud = storyDialogueHUD;
            if (preferredHud == null)
                preferredHud = FindUniqueInactive<StoryDialogueHUD>(ref _ambiguousHudWarned);

            if (preferredHud != null && preferredHud.IsPresentationReady &&
                (!EventRequiresChoices(_currentEvent) || preferredHud.CanPresentChoices))
            {
                storyDialogueHUD = preferredHud;
                _presenter = PresenterKind.StoryDialogueHud;
                UnbindDialoguePanelAdvance();
                return;
            }

            DialoguePanel fallbackPanel = dialoguePanel;
            if (fallbackPanel == null)
                fallbackPanel = FindUniqueInactive<DialoguePanel>(ref _ambiguousPanelWarned);

            if (fallbackPanel != null && fallbackPanel.IsPresentationReady)
            {
                dialoguePanel = fallbackPanel;
                _presenter = PresenterKind.DialoguePanel;
                BindDialoguePanelAdvance();
            }
        }

        private T FindUniqueInactive<T>(ref bool warned) where T : Component
        {
            T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (candidates.Length == 1)
                return candidates[0];

            if (candidates.Length > 1 && !warned)
            {
                warned = true;
                Debug.LogWarning(
                    $"[StoryEventRunner] Ambiguous {typeof(T).Name} binding ({candidates.Length} candidates). Assign the preferred presenter explicitly.",
                    this);
            }

            return null;
        }

        private void BindDialoguePanelAdvance()
        {
            if (dialoguePanel == null)
                return;
            dialoguePanel.OnNextRequested -= Advance;
            dialoguePanel.OnNextRequested += Advance;
        }

        private void UnbindDialoguePanelAdvance()
        {
            if (dialoguePanel != null)
                dialoguePanel.OnNextRequested -= Advance;
        }

        private void HideChoices()
        {
            if (_presenter == PresenterKind.StoryDialogueHud)
                storyDialogueHUD?.HideChoices();
            else if (_presenter == PresenterKind.DialoguePanel)
                dialoguePanel?.ClearChoices();
        }

        private void HideOwnedPresenter()
        {
            if (_presenter == PresenterKind.StoryDialogueHud)
                storyDialogueHUD?.HideAll();
            else if (_presenter == PresenterKind.DialoguePanel)
            {
                UnbindDialoguePanelAdvance();
                dialoguePanel?.Hide();
            }

            _presenter = PresenterKind.None;
        }

        private void ClearRunData()
        {
            _currentEvent = null;
            _currentNode = null;
            _currentSpeakerAnchor = null;
            _resolvedChoices.Clear();
            _choiceTimerRunning = false;
            _choiceTimeoutAt = 0f;
            _nodeToken++;
        }

        private bool IsCurrentNode(int token)
        {
            return IsRunning && token == _nodeToken && _currentNode != null;
        }

        private bool ContainsCurrentChoice(StoryChoice choice)
        {
            if (_currentNode?.Choices == null)
                return false;
            for (int i = 0; i < _currentNode.Choices.Count; i++)
            {
                if (ReferenceEquals(_currentNode.Choices[i], choice))
                    return true;
            }
            return false;
        }

        private bool TryFindResolvedChoice(StoryChoice choice, out ResolvedStoryChoice resolved)
        {
            for (int i = 0; i < _resolvedChoices.Count; i++)
            {
                if (ReferenceEquals(_resolvedChoices[i].Choice, choice))
                {
                    resolved = _resolvedChoices[i];
                    return true;
                }
            }

            resolved = default;
            return false;
        }

        private static bool EventRequiresChoices(StoryEventDefinitionSO eventDefinition)
        {
            if (eventDefinition?.Nodes == null)
                return false;

            for (int i = 0; i < eventDefinition.Nodes.Count; i++)
            {
                StoryNode node = eventDefinition.Nodes[i];
                if (node?.Choices != null && node.Choices.Count > 0)
                    return true;
            }

            return false;
        }

        private static bool HasSelectableChoice(IReadOnlyList<ResolvedStoryChoice> choices)
        {
            if (choices == null) return false;
            for (int i = 0; i < choices.Count; i++)
                if (choices[i].IsEnabled) return true;
            return false;
        }

        private bool HasValidTimerRoute(StoryNode node, IReadOnlyList<ResolvedStoryChoice> choices)
        {
            if (node == null || !node.UseTimedChoices || node.ChoiceTimeLimitSeconds <= 0f)
                return false;

            if (!string.IsNullOrEmpty(node.TimeoutNodeId) && _currentEvent?.GetNode(node.TimeoutNodeId) != null)
                return true;

            int index = node.TimeoutChoiceIndex;
            return index >= 0 && index < choices.Count && choices[index].IsEnabled;
        }

        private void ContinueWithoutChoice(StoryNode sourceNode)
        {
            _lifecycle = StoryRunLifecycle.ShowingDialogue;
            HideChoices();
            if (sourceNode != null && !string.IsNullOrEmpty(sourceNode.NextNodeId))
            {
                StoryNode next = _currentEvent?.GetNode(sourceNode.NextNodeId);
                if (next != null)
                {
                    ShowNode(next);
                    return;
                }
            }

            EndEvent();
        }

        private bool IsNarrativeAdvanceState()
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            return stateMachine != null &&
                   (stateMachine.Current == GameState.Dialogue || stateMachine.Current == GameState.Cutscene);
        }

        private void WarnMissingFlow(StoryEventDefinitionSO eventDefinition)
        {
            if (_missingFlowWarned)
                return;
            _missingFlowWarned = true;
            Debug.LogWarning(
                $"[StoryEventRunner] Production story start requires GameStateMachine and GameFlowController. event='{eventDefinition?.EventId}'.",
                this);
        }

        private static void PruneActiveRunner()
        {
            if (_activeRunner == null || !_activeRunner.IsRunning)
                _activeRunner = null;
        }

        private void ReleaseActiveOwnership()
        {
            if (_activeRunner == this)
                _activeRunner = null;
        }

        private void EnsureInputSubscription()
        {
            GameInputInstaller installer = global::GameInputInstaller.Instance;
            InputService service = installer != null ? installer.Service : null;
            if (_inputSubscribed && _inputInstaller == installer && _inputService == service)
                return;

            UnsubscribeInput();
            if (installer == null || service == null)
                return;

            _inputInstaller = installer;
            _inputService = service;
            _inputService.DialogueAdvance += HandleAdvanceInput;
            _inputService.NarrativeChoiceSelected += HandleChoiceInput;
            _inputSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (_inputSubscribed && _inputService != null)
            {
                _inputService.DialogueAdvance -= HandleAdvanceInput;
                _inputService.NarrativeChoiceSelected -= HandleChoiceInput;
            }
            _inputSubscribed = false;
            _inputInstaller = null;
            _inputService = null;
        }

        private void HandleAdvanceInput()
        {
            Advance();
        }

        private void HandleChoiceInput(int visibleIndex)
        {
            SelectChoiceByIndex(visibleIndex);
        }

        private static bool IsChoiceState()
        {
            return GameStateMachine.Instance != null && GameStateMachine.Instance.Current == GameState.Choice;
        }

        internal static void ResetActiveOwnershipForTests()
        {
            _activeRunner = null;
            _nextGeneration = 0;
        }
    }
}
