using System;
using System.Collections;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI
{
    /// <summary>
    /// Renders FinalExchange player decisions inside the canonical combat root and forwards
    /// commands exclusively through FinalCombatPlayerCommandController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FinalCombatUIBinder : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject actorListRoot;
        [SerializeField] private GameObject skillListRoot;
        [SerializeField] private GameObject targetListRoot;
        [SerializeField] private GameObject handoffListRoot;

        [Header("Dynamic Button")]
        [SerializeField] private Button optionButtonPrefab;

        [Header("Commands")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button noResponseButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button handoffButton;
        [SerializeField] private Button endButton;

        [Header("Status")]
        [SerializeField] private Font displayFont;
        [SerializeField] private Text phaseText;
        [SerializeField] private TMP_Text contextText;
        [SerializeField] private Text playerHpText;
        [SerializeField] private Text enemyHpText;
        [SerializeField] private Text playerMpText;
        [SerializeField] private Text enemyMpText;
        [SerializeField] private Text pressureText;
        [SerializeField] private Text playerPostureText;
        [SerializeField] private Text enemyPostureText;

        [Header("Optional Status Presentation")]
        [SerializeField] private FinalCombatStatusListPresenter statusListPresenter;

        [Header("Optional Reference Layout")]
        [SerializeField] private Button actMenuButton;
        [SerializeField] private Button skillMenuButton;
        [SerializeField] private Button itemsMenuButton;
        [SerializeField] private GameObject itemPanelRoot;
        [SerializeField] private GameObject itemListRoot;
        [SerializeField] private Sprite cardFrameSprite;
        [SerializeField] private TMP_Text combatDetailsText;

        [Serializable]
        private sealed class SkillArtwork
        {
            public int skillId;
            public Sprite artwork;
        }

        [Serializable]
        private sealed class ItemArtwork
        {
            public string itemId;
            public Sprite artwork;
        }

        [Header("Optional Authored Artwork")]
        [SerializeField] private SkillArtwork[] skillArtwork = Array.Empty<SkillArtwork>();
        [SerializeField] private ItemArtwork[] itemArtwork = Array.Empty<ItemArtwork>();

        private enum MenuSection { Act, Skill, Items }
        private MenuSection _menuSection;
        private bool UsesReferenceLayout => actMenuButton != null && skillMenuButton != null &&
            itemsMenuButton != null && itemPanelRoot != null && itemListRoot != null;
        private bool UsesVideoDesign => UsesReferenceLayout && panelRoot != null &&
            panelRoot.transform.Find("ReferenceFilmBed") != null;

        private readonly List<Button> _spawnedButtons = new List<Button>();
        private readonly List<Button> _closingItemButtons = new List<Button>();
        private readonly Dictionary<long, float> _cardOffsets = new Dictionary<long, float>();
        private FinalCombatPlayerCommandController _controller;
        private CombatSession _session;
        private CombatStateMachine _stateMachine;
        private CombatFlowOrchestrator _orchestrator;
        private PlayerCombatDecisionViewState _viewState = PlayerCombatDecisionViewState.Empty;
        private bool _staticButtonsBound;
        private int _lastActingMp = -1;
        private int _lastActingBp = -1;
        private int _lastBpRoundCycle = -1;
        private int _lastUltimateStage = -1;
        private int _lastPlayerHp = -1;
        private int _lastEnemyHp = -1;
        private int _lastAllOutWiringErrorVersion = -1;
        private int _displayedExchangeIndex;
        private string _lastClashFeedback;
        private string _cardCommitFeedback;
        private readonly HashSet<long> _shownCommittedCards = new HashSet<long>();
        private int _commitFeedbackExchange;
        private string _lastComparisonFeedback;
        private CombatClashSeriesState _feedbackSeries;
        private int _lastFeedbackComparison;
        private bool _showExchangeFeedback;
        private Coroutine _entranceAnimation;
        private Coroutine _itemAnimation;
        private Coroutine _menuAnimation;
        private RectTransform _statusTransform;
        private RectTransform _menuTransform;
        private RectTransform _filmTransform;
        private RectTransform _commandsTransform;
        private RectTransform _itemViewport;
        private RectTransform _videoItemReel;
        private RectTransform _videoItemReelBack;
        private float _itemReveal;
        private Vector2 _statusPosition;
        private Vector2 _menuPosition;
        private Vector2 _filmPosition;
        private Vector2 _commandsPosition;
        private Vector2 _itemPosition;
        private CanvasGroup _statusGroup;
        private CanvasGroup _menuGroup;
        private CanvasGroup _filmGroup;
        private CanvasGroup _commandsGroup;
        private CanvasGroup _itemGroup;
        private bool _animationReady;
        private bool _itemsRequested;
        private MenuSection? _lastAnimatedMenu;
        private Color _itemOverlayColor;
        private CombatDirector _presentationDirector;
        private CombatUITransientPresentation _standoffPresentation;
        private readonly List<CombatUITransientPresentation> _presentationEffects = new();
        private int _lastStandoffPresentationVersion = -1;

        public bool IsBound => _controller != null && _session != null;
        public PlayerCombatDecisionViewState ViewState => _viewState;

        private void Awake()
        {
            BindStaticButtons();
            ApplyDisplayFont();
        }

        private void OnEnable()
        {
            BindStaticButtons();
            ApplyDisplayFont();
        }

        private void OnDisable()
        {
            UnbindStaticButtons();
            Unbind();
        }

        private void Update()
        {
            if (!IsBound)
                return;

            ObserveCombatFeedback();
            ICombatant actor = _viewState.ActingActor ?? _viewState.SelectedActor;
            if (actor != null && _session.TryGetCombatState(actor, out CombatantCombatState state) &&
                state.CurrentMp != _lastActingMp)
            {
                _lastActingMp = state.CurrentMp;
                _controller.RefreshAvailability();
            }

            CombatBpRuntime bp = _session.BpRuntime;
            if (bp != null && bp.TryGetBalance(actor, out int currentBp) &&
                (currentBp != _lastActingBp || bp.RoundCycleCount != _lastBpRoundCycle))
            {
                _lastActingBp = currentBp;
                _lastBpRoundCycle = bp.RoundCycleCount;
                _controller.RefreshAvailability();
                RefreshStatus();
            }

            ICombatant player = _viewState.SelectedHandoffTarget ??
                (_viewState.ActingActor?.Side == Side.Allies ? _viewState.ActingActor : null) ??
                _viewState.SelectedActor ?? FindFirst(_session.Allies);
            ICombatant statusEnemy = FindStatusEnemy();
            if ((player?.HP ?? -1) != _lastPlayerHp || (statusEnemy?.HP ?? -1) != _lastEnemyHp)
                RefreshStatus();
            if (_session.UltimateRuntime != null &&
                _session.UltimateRuntime.TryGetStage(player, out int ultimateStage) &&
                ultimateStage != _lastUltimateStage)
            {
                _lastUltimateStage = ultimateStage;
                Render();
            }

            if (_controller.IsAllOutCandidate || _viewState.ClashSeriesToken.HasValue ||
                _session.ExchangeState.ClashSeries != null && _stateMachine?.Phase == Phase.Clash)
                RefreshStatus();
        }

        public void Bind(
            CombatSession session,
            CombatStateMachine stateMachine,
            CombatFlowOrchestrator orchestrator)
        {
            if (ReferenceEquals(_session, session) && ReferenceEquals(_stateMachine, stateMachine) && IsBound)
                return;

            Unbind();
            if (session == null || stateMachine == null || orchestrator == null ||
                session.FlowMode != CombatFlowMode.StandoffClashChain)
            {
                return;
            }

            _session = session;
            _stateMachine = stateMachine;
            _orchestrator = orchestrator;
            _displayedExchangeIndex = session.CardExchangeIndex;
            _lastClashFeedback = null;
            _showExchangeFeedback = false;
            _controller = new FinalCombatPlayerCommandController(new SessionHandoffEligibilityProvider());
            _controller.ViewStateChanged += HandleViewStateChanged;
            _stateMachine.OnPhaseChanged += HandlePhaseChanged;
            _session.ExchangeState.OnStateChanged += HandleExchangeStateChanged;
            _session.CardCommitted += HandleCardCommitted;
            _orchestrator.ClashFeedbackAvailable += HandleClashFeedback;
            if (!_controller.Bind(orchestrator, session))
            {
                Unbind();
                return;
            }

            _viewState = _controller.ViewState;
            TrackClashSeries();
            if (_feedbackSeries != null)
                HandleClashFeedback(_feedbackSeries, _orchestrator.PendingClashRoundPresentationRequest?.Result);
            SetVisible(panelRoot, true);
            Render();
            StartEntranceAnimation();
            _presentationDirector = orchestrator.GetComponent<CombatDirector>();
            if (_presentationDirector != null)
                _presentationDirector.FinalPresentationCueRaised += HandlePresentationCue;
            if (stateMachine.Phase == Phase.Standoff)
                PresentStandoff();
        }

        public void Unbind()
        {
            if (_presentationDirector != null)
                _presentationDirector.FinalPresentationCueRaised -= HandlePresentationCue;
            _presentationDirector = null;
            foreach (CombatUITransientPresentation effect in _presentationEffects)
                if (effect != null) effect.Dismiss();
            _presentationEffects.Clear();
            _standoffPresentation = null;
            _lastStandoffPresentationVersion = -1;
            ResetPresentation();
            if (_stateMachine != null)
                _stateMachine.OnPhaseChanged -= HandlePhaseChanged;
            if (_orchestrator != null)
                _orchestrator.ClashFeedbackAvailable -= HandleClashFeedback;
            if (_session != null)
            {
                _session.ExchangeState.OnStateChanged -= HandleExchangeStateChanged;
                _session.CardCommitted -= HandleCardCommitted;
            }
            if (_controller != null)
            {
                _controller.ViewStateChanged -= HandleViewStateChanged;
                _controller.Unbind();
            }

            _controller = null;
            _session = null;
            _stateMachine = null;
            _orchestrator = null;
            _lastActingMp = -1;
            _lastActingBp = -1;
            _lastBpRoundCycle = -1;
            _lastUltimateStage = -1;
            _lastPlayerHp = -1;
            _lastEnemyHp = -1;
            _lastAllOutWiringErrorVersion = -1;
            _displayedExchangeIndex = 0;
            _lastClashFeedback = null;
            _cardCommitFeedback = null;
            _shownCommittedCards.Clear();
            _commitFeedbackExchange = 0;
            _lastComparisonFeedback = null;
            _feedbackSeries = null;
            _lastFeedbackComparison = 0;
            _showExchangeFeedback = false;
            _viewState = PlayerCombatDecisionViewState.Empty;
            _menuSection = MenuSection.Act;
            ClearDynamicButtons();
            ClearClosingItems();
            _cardOffsets.Clear();
            statusListPresenter?.Clear();
            SetVisible(itemPanelRoot, false);
            SetVisible(panelRoot, false);
        }

        public void ApplyPhase(Phase phase)
        {
            if (!IsBound)
                return;

            ObserveCombatFeedback();
            RefreshStatus();
            Render();
        }

        private void HandlePhaseChanged(Phase previous, Phase next)
        {
            if (next != Phase.Standoff && _standoffPresentation != null)
                _standoffPresentation.Dismiss();
            if (next == Phase.ExitCombat)
            {
                _cardCommitFeedback = null;
                _shownCommittedCards.Clear();
                _feedbackSeries = null;
                _lastFeedbackComparison = 0;
                _lastComparisonFeedback = null;
                _lastClashFeedback = null;
                _showExchangeFeedback = false;
            }
            ApplyPhase(next);
            if (next == Phase.Standoff)
                PresentStandoff();
            if (previous == Phase.Clash || next == Phase.Clash)
                TraceClashUi("PhaseChanged");
        }

        private void HandleViewStateChanged(PlayerCombatDecisionViewState state)
        {
            _viewState = state ?? PlayerCombatDecisionViewState.Empty;
            if (_viewState.SelectedCardRuntimeId.HasValue)
                _showExchangeFeedback = false;
            Render();
            if (_stateMachine?.Phase == Phase.Clash)
                TraceClashUi("ViewStateChanged");
        }

        private void HandleExchangeStateChanged(int version)
        {
            TrackClashSeries();
            RefreshStatus();
            if (_stateMachine?.Phase == Phase.Clash)
                TraceClashUi("ExchangeStateChanged");
        }

        private void TraceClashUi(string boundary)
        {
#if UNITY_EDITOR
            if (_session == null || _orchestrator == null)
                return;

            CombatClashSeriesState series = _session.ExchangeState?.ClashSeries;
            CombatClashRoundPresentationRequest request =
                _orchestrator.PendingClashRoundPresentationRequest;
            Debug.Log($"[ClashTrace/UI] {boundary} session={_session.CompletionId} " +
                $"exchange={_session.ExchangeState?.DeclarationId}/{_session.ExchangeState?.Version} " +
                $"round={series?.ComparisonCount} token={series?.CommandToken} " +
                $"phase={_stateMachine?.Phase} gameState={GameStateMachine.Instance?.Current.ToString() ?? "<none>"} " +
                $"timeScale={Time.timeScale:0.###} request={request?.GetHashCode()} " +
                $"decision={_viewState.DecisionKind} title={phaseText?.text} context={contextText?.text}", this);
#endif
        }

        private void Render()
        {
            Phase phase = _stateMachine != null ? _stateMachine.Phase : Phase.EnterCombat;
            bool automaticPhase = IsAutomaticPresentationPhase(phase);
            bool series = _viewState.DecisionKind == CombatExchangeDecisionKind.ClashAttack ||
                          _viewState.DecisionKind == CombatExchangeDecisionKind.ClashResponse;
            bool hasRequest = IsBound && phase != Phase.ExitCombat && _viewState.DecisionKind.HasValue &&
                              (!automaticPhase || phase == Phase.Clash && series);
            bool attack = _viewState.DecisionKind == CombatExchangeDecisionKind.Attack;
            bool response = _viewState.DecisionKind == CombatExchangeDecisionKind.Response;
            bool chain = _viewState.DecisionKind == CombatExchangeDecisionKind.Chain;

            // Menu navigation only changes presentation; the existing command controller owns selection.
            bool canShowItems = hasRequest && attack && _viewState.CanUseItem &&
                _viewState.SelectableItems.Count > 0;
            if (hasRequest && attack && _viewState.SelectedActor == null)
                _menuSection = MenuSection.Act;
            else if (hasRequest && !attack || _menuSection == MenuSection.Items && !canShowItems)
                _menuSection = MenuSection.Skill;
            bool itemsOpen = UsesReferenceLayout && canShowItems && _menuSection == MenuSection.Items;
            if (UsesVideoDesign) SetVisible(panelRoot.transform.Find("ReferenceMenu").gameObject, hasRequest && !itemsOpen);
            if (UsesVideoDesign && targetListRoot != null && targetListRoot.transform.parent is RectTransform targets)
            {
                targets.anchorMin = targets.anchorMax = targets.pivot = itemsOpen ? Vector2.zero : Vector2.right;
                targets.anchoredPosition = itemsOpen ? new Vector2(80, 450) : new Vector2(-36, 294);
                targets.sizeDelta = itemsOpen ? new Vector2(560, 70) : new Vector2(760, 70);
            }
            SetItemPresentation(itemsOpen);
            if (UsesVideoDesign && !hasRequest) SetVisible(itemPanelRoot, false);
            SetVisible(itemListRoot, itemsOpen || _animationReady && itemPanelRoot.activeSelf);
            SetMenuButton(actMenuButton, hasRequest, attack, _menuSection == MenuSection.Act);
            SetMenuButton(skillMenuButton, hasRequest, true, _menuSection == MenuSection.Skill);
            SetMenuButton(itemsMenuButton, hasRequest, canShowItems, itemsOpen);
            AnimateMenuChange(hasRequest);

            ClearDynamicButtons();
            if (!hasRequest || !_viewState.HasHand)
                _cardOffsets.Clear();
            bool showUltimate = hasRequest && (attack || chain) &&
                _session?.UltimateRuntime != null;
            SetListVisible(actorListRoot, hasRequest && attack &&
                (_viewState.SelectableActors.Count > 0 || showUltimate) &&
                (!UsesReferenceLayout || _menuSection == MenuSection.Act));
            SetListVisible(skillListRoot, hasRequest && (attack || response || chain || series) &&
                (_viewState.HandCards.Count > 0 || _viewState.SkillAvailability.Count > 0 ||
                 _viewState.SelectableSkills.Count > 0));
            SetListVisible(targetListRoot, hasRequest && (attack || response) && _viewState.SelectableTargets.Count > 0);
            bool allOut = hasRequest && chain && _controller.IsAllOutCandidate;
            SetListVisible(handoffListRoot, hasRequest &&
                (chain && (_session.Allies.Count > 1 || allOut) ||
                 attack && (!UsesReferenceLayout && _viewState.SelectableItems.Count > 0 || _viewState.CanOvercome ||
                            _viewState.SelectedSkill != null && _session?.BpRuntime != null) ||
                 response && _viewState.SelectedSkill != null && _session?.BpRuntime != null ||
                 series && _viewState.SelectedSkill != null && _session?.BpRuntime != null ||
                 showUltimate && chain) && !itemsOpen);

            if (hasRequest && attack)
                BuildCombatantOptions(actorListRoot, _viewState.SelectableActors, _viewState.SelectedActor, HandleActorSelected);
            if (hasRequest && (attack || response || series))
            {
                if (_viewState.HasHand)
                    BuildHandOptions(skillListRoot, _viewState);
                else
                    BuildAvailabilityOptions(skillListRoot, _viewState.SkillAvailability,
                        _viewState.SelectedSkill);
            }
            else if (hasRequest && chain)
                BuildSkillOptions(skillListRoot, _viewState.SelectableSkills,
                    _viewState.SelectedSkill, HandleSkillSelected);
            if (hasRequest && (attack || response || series))
                BuildBpInvestmentOptions(handoffListRoot);
            if (hasRequest && attack && (!UsesReferenceLayout || itemsOpen))
                BuildCombatItemOptions(UsesReferenceLayout ? itemListRoot : handoffListRoot,
                    _viewState.SelectableItems, _viewState.SelectedItem);
            if (hasRequest && attack && _viewState.CanOvercome)
                BuildOvercomeOption(handoffListRoot);
            if (hasRequest && (attack || response))
                BuildCombatantOptions(targetListRoot, _viewState.SelectableTargets, _viewState.SelectedTarget, HandleTargetSelected);
            if (hasRequest && chain && !allOut)
                BuildHandoffOptions(handoffListRoot);

            if (allOut)
            {
                if ((handoffListRoot == null || contextText == null || optionButtonPrefab == null) &&
                    _lastAllOutWiringErrorVersion != _viewState.ExchangeVersion)
                {
                    _lastAllOutWiringErrorVersion = _viewState.ExchangeVersion;
                    Debug.LogError("[FinalCombatUIBinder] All-Out requires handoffListRoot, contextText and optionButtonPrefab connections.", this);
                }
                BuildAllOutOption(handoffListRoot);
            }
            if (showUltimate && !allOut)
                BuildUltimateOption(attack ? actorListRoot : handoffListRoot);

            SetCommandButton(confirmButton, hasRequest && (attack || response || series) && _viewState.CanConfirm);
            SetCommandButton(cancelButton, hasRequest && (attack || response || chain || series));
            SetCommandButton(noResponseButton, hasRequest && (response || series) && _viewState.CanNoResponse);
            SetCommandButton(continueButton, hasRequest && chain && _viewState.CanContinue);
            SetCommandButton(handoffButton, hasRequest && chain && _viewState.CanHandoff);
            SetCommandButton(endButton, hasRequest && chain && !allOut && _viewState.CanEnd);
            RefreshStatus();
        }

        private static CanvasGroup PresentationGroup(RectTransform target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        private void TrackPresentation(CombatUITransientPresentation effect)
        {
            _presentationEffects.RemoveAll(item => item == null);
            if (effect != null) _presentationEffects.Add(effect);
        }

        private void PresentStandoff()
        {
            if (!PreparePresentation() || _stateMachine?.Phase != Phase.Standoff ||
                _lastStandoffPresentationVersion == _session.ExchangeState.Version) return;
            _lastStandoffPresentationVersion = _session.ExchangeState.Version;
            _standoffPresentation = CombatUITransientPresentation.Banner((RectTransform)panelRoot.transform,
                displayFont, $"standoff  #{_session.CardExchangeIndex}", UsesVideoDesign ? 2.9f : 0.7f);
            TrackPresentation(_standoffPresentation);
        }

        private bool IsSessionMember(ICombatant combatant)
        {
            if (combatant == null || _session == null) return false;
            foreach (ICombatant member in _session.GetSide(combatant.Side))
                if (ReferenceEquals(member, combatant)) return true;
            return false;
        }

        private void HandlePresentationCue(CombatFinalPresentationCue cue)
        {
            if (!IsBound || cue == null || !PreparePresentation()) return;
            if (cue.Kind == CombatFinalPresentationCueKind.Standoff)
            {
                PresentStandoff();
                return;
            }

            ICombatant subject;
            Color color;
            switch (cue.Kind)
            {
                case CombatFinalPresentationCueKind.AttackAction:
                case CombatFinalPresentationCueKind.ResponseAction:
                case CombatFinalPresentationCueKind.WinningSkill:
                case CombatFinalPresentationCueKind.AllOut:
                    subject = cue.Actor;
                    color = new Color(0.35f, 0.7f, 1f, 0.45f);
                    break;
                case CombatFinalPresentationCueKind.HitReaction:
                case CombatFinalPresentationCueKind.Defeat:
                    subject = cue.Target;
                    color = new Color(1f, 0.2f, 0.18f, 0.5f);
                    break;
                case CombatFinalPresentationCueKind.PostureReaction:
                case CombatFinalPresentationCueKind.StunReaction:
                    subject = cue.Target;
                    color = new Color(1f, 0.7f, 0.2f, 0.45f);
                    break;
                default:
                    return;
            }
            if (!IsSessionMember(subject)) return;
            Text label = subject.Side == Side.Allies ? playerHpText : enemyHpText;
            if (label == null) return;
            TrackPresentation(CombatUITransientPresentation.Flash(label.transform.parent as RectTransform, color));
        }

        private void PresentCommittedCard(CombatHandCard card)
        {
            if (!PreparePresentation() || card.Skill == null || optionButtonPrefab == null) return;
            Button visual = CreateOption(panelRoot.transform, card.Skill.Name);
            if (visual == null) return;
            _spawnedButtons.Remove(visual);
            visual.name = "CommittedFilmCard";
            visual.gameObject.SetActive(true);
            StyleCard(visual, card.Skill, "사용 확정", true, true);
            Text state = visual.transform.Find("FilmFace/CostPlate/CardState")?.GetComponent<Text>();
            if (state != null) state.text = "실행";
            // The visual is independent of the regenerated choices and never submits a command.
            RectTransform slot = visual.transform.parent as RectTransform;
            if (slot == null || slot == panelRoot.transform)
            {
                Destroy(visual.gameObject);
                return;
            }
            slot.name = "CommittedFilmCardPresentation";
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.sizeDelta = UsesVideoDesign ? new Vector2(275f, 365f) : new Vector2(142f, 154f);
            slot.anchoredPosition = UsesVideoDesign ? new Vector2(-790f, 700f) : new Vector2(0f, 28f);
            if (UsesVideoDesign) slot.localEulerAngles = new Vector3(0f, 0f, -7f);
            TrackPresentation(CombatUITransientPresentation.Card(slot));
        }

        internal CombatUITransientPresentation PresentCompletion(CombatResult result)
        {
            if (!Application.isPlaying || !UsesReferenceLayout || result == null ||
                result.EndReason == CombatEndReason.Abort) return null;
            Canvas canvas = GetComponentInParent<Canvas>(true);
            Game.UI.GameUIRootController owner = GetComponentInParent<Game.UI.GameUIRootController>(true);
            if (owner != null)
            {
                Game.UI.RewardUIPanel[] rewards = owner.GetComponentsInChildren<Game.UI.RewardUIPanel>(true);
                if (rewards.Length != 1) return null;
                canvas = rewards[0].GetComponentInParent<Canvas>(true);
            }
            if (canvas == null) return null;
            return CombatUITransientPresentation.Completion((RectTransform)canvas.transform, displayFont, result);
        }

        private bool PreparePresentation()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return false;
            if (_animationReady) return true;
            if (!UsesReferenceLayout || panelRoot == null) return false;
            Transform root = panelRoot.transform;
            _statusTransform = root.Find("Status") as RectTransform;
            _menuTransform = root.Find("ReferenceMenu") as RectTransform;
            _filmTransform = root.Find("FilmCardViewport") as RectTransform;
            _commandsTransform = root.Find("CommandButtons") as RectTransform;
            _itemViewport = itemPanelRoot.transform.Find("ItemViewport") as RectTransform;
            if (UsesVideoDesign)
            {
                _videoItemReel = itemPanelRoot.transform.Find("ItemReel") as RectTransform;
                _videoItemReelBack = itemPanelRoot.transform.Find("ItemReelBlue") as RectTransform;
            }
            Image overlay = itemPanelRoot.GetComponent<Image>();
            if (_statusTransform == null || _menuTransform == null || _filmTransform == null || _commandsTransform == null ||
                _itemViewport == null || overlay == null) return false;
            _statusPosition = _statusTransform.anchoredPosition;
            _menuPosition = _menuTransform.anchoredPosition;
            _filmPosition = _filmTransform.anchoredPosition;
            _commandsPosition = _commandsTransform.anchoredPosition;
            _itemPosition = _itemViewport.anchoredPosition;
            _itemOverlayColor = overlay.color;
            _statusGroup = PresentationGroup(_statusTransform);
            _menuGroup = PresentationGroup(_menuTransform);
            _filmGroup = PresentationGroup(_filmTransform);
            _commandsGroup = PresentationGroup(_commandsTransform);
            _itemGroup = PresentationGroup(_itemViewport);
            _animationReady = true;
            return true;
        }

        private void StartEntranceAnimation()
        {
            if (!PreparePresentation()) return;
            if (_entranceAnimation != null) StopCoroutine(_entranceAnimation);
            _entranceAnimation = StartCoroutine(AnimateEntrance());
        }

        private IEnumerator AnimateEntrance()
        {
            float duration = UsesVideoDesign ? 0.5f : 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                _statusTransform.anchoredPosition = _statusPosition + Vector2.up * (24f * (1f - eased));
                _menuTransform.anchoredPosition = _menuPosition + Vector2.left * (24f * (1f - eased));
                _filmTransform.anchoredPosition = _filmPosition + Vector2.down * (190f * (1f - eased));
                _commandsTransform.anchoredPosition = _commandsPosition + Vector2.right * (24f * (1f - eased));
                _statusGroup.alpha = _menuGroup.alpha = _filmGroup.alpha = _commandsGroup.alpha = eased;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _statusTransform.anchoredPosition = _statusPosition;
            _menuTransform.anchoredPosition = _menuPosition;
            _filmTransform.anchoredPosition = _filmPosition;
            _commandsTransform.anchoredPosition = _commandsPosition;
            _statusGroup.alpha = _menuGroup.alpha = _filmGroup.alpha = _commandsGroup.alpha = 1f;
            _entranceAnimation = null;
        }

        private void SetItemPresentation(bool visible)
        {
            if (!PreparePresentation())
            {
                SetVisible(itemPanelRoot, visible);
                return;
            }
            if (_itemsRequested == visible) return;
            _itemsRequested = visible;
            if (_itemAnimation != null) StopCoroutine(_itemAnimation);
            if (visible) ClearClosingItems();
            SetVisible(itemPanelRoot, true);
            _itemAnimation = StartCoroutine(AnimateItems(visible));
        }

        private IEnumerator AnimateItems(bool opening)
        {
            float duration = UsesVideoDesign ? 0.65f : 0.16f;
            Image overlay = itemPanelRoot.GetComponent<Image>();
            float fromAlpha = overlay.color.a;
            float fromListAlpha = _itemGroup.alpha;
            Vector2 fromPosition = _itemViewport.anchoredPosition;
            float targetAlpha = opening ? _itemOverlayColor.a : 0f;
            Vector2 travel = UsesVideoDesign ? new Vector2(640, 120) : Vector2.right * 35f;
            Vector2 targetPosition = _itemPosition + (opening ? Vector2.zero : travel);
            float fromReveal = _itemReveal;
            if (opening && fromAlpha == _itemOverlayColor.a && fromListAlpha == 1f)
            {
                fromAlpha = fromListAlpha = 0f;
                fromPosition = _itemPosition + travel;
            }
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                Color color = _itemOverlayColor;
                color.a = Mathf.Lerp(fromAlpha, targetAlpha, t);
                overlay.color = color;
                _itemGroup.alpha = Mathf.Lerp(fromListAlpha, opening ? 1f : 0f, t);
                _itemViewport.anchoredPosition = Vector2.Lerp(fromPosition, targetPosition, t);
                if (UsesVideoDesign) SetVideoItemReel(Mathf.SmoothStep(fromReveal, opening ? 1f : 0f, t));
                yield return null;
            }
            Color final = _itemOverlayColor;
            final.a = targetAlpha;
            overlay.color = final;
            _itemGroup.alpha = opening ? 1f : 0f;
            _itemViewport.anchoredPosition = targetPosition;
            if (UsesVideoDesign) SetVideoItemReel(opening ? 1f : 0f);
            if (!opening)
            {
                ClearClosingItems();
                SetVisible(itemListRoot, false);
                SetVisible(itemPanelRoot, false);
            }
            _itemAnimation = null;
        }

        private void SetVideoItemReel(float reveal)
        {
            _itemReveal = reveal;
            if (_videoItemReel != null)
            {
                _videoItemReel.pivot = new Vector2(.5f, .5f);
                _videoItemReel.anchoredPosition = Vector2.Lerp(new Vector2(438, 430), new Vector2(120, 80), reveal);
                _videoItemReel.sizeDelta = Vector2.one * Mathf.Lerp(154, 520, reveal);
                _videoItemReel.localEulerAngles = new Vector3(0, 0, 60 * reveal);
            }
            if (_videoItemReelBack != null)
            {
                _videoItemReelBack.pivot = new Vector2(.5f, .5f);
                _videoItemReelBack.anchoredPosition = Vector2.Lerp(new Vector2(438, 430), new Vector2(120, 80), reveal);
                _videoItemReelBack.sizeDelta = Vector2.one * Mathf.Lerp(148, 504, reveal);
            }
        }

        private void AnimateMenuChange(bool visible)
        {
            if (!PreparePresentation() || !visible) return;
            if (_lastAnimatedMenu == _menuSection) return;
            _lastAnimatedMenu = _menuSection;
            if (_menuAnimation != null) StopCoroutine(_menuAnimation);
            foreach (Button menuButton in new[] { actMenuButton, skillMenuButton, itemsMenuButton })
            {
                Transform existingMarker = menuButton.transform.Find("MenuWedge");
                if (existingMarker != null) existingMarker.localScale = Vector3.one;
            }
            Button button = _menuSection == MenuSection.Act ? actMenuButton :
                _menuSection == MenuSection.Skill ? skillMenuButton : itemsMenuButton;
            _menuAnimation = StartCoroutine(AnimateMenuMarker(button));
        }

        private IEnumerator AnimateMenuMarker(Button button)
        {
            RectTransform marker = button.transform.Find("MenuWedge") as RectTransform;
            if (marker == null)
            {
                _menuAnimation = null;
                yield break;
            }
            const float duration = 0.14f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                marker.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, elapsed / duration);
                yield return null;
            }
            marker.localScale = Vector3.one;
            _menuAnimation = null;
        }

        private void ResetPresentation()
        {
            if (!_animationReady) return;
            if (_entranceAnimation != null) StopCoroutine(_entranceAnimation);
            if (_itemAnimation != null) StopCoroutine(_itemAnimation);
            if (_menuAnimation != null) StopCoroutine(_menuAnimation);
            _entranceAnimation = _itemAnimation = _menuAnimation = null;
            _statusTransform.anchoredPosition = _statusPosition;
            _menuTransform.anchoredPosition = _menuPosition;
            _filmTransform.anchoredPosition = _filmPosition;
            _commandsTransform.anchoredPosition = _commandsPosition;
            _itemViewport.anchoredPosition = _itemPosition;
            _statusGroup.alpha = _menuGroup.alpha = _filmGroup.alpha = _commandsGroup.alpha = _itemGroup.alpha = 1f;
            itemPanelRoot.GetComponent<Image>().color = _itemOverlayColor;
            foreach (Button button in new[] { actMenuButton, skillMenuButton, itemsMenuButton })
            {
                Transform marker = button.transform.Find("MenuWedge");
                if (marker != null) marker.localScale = Vector3.one;
            }
            _itemsRequested = false;
            _itemReveal = 0f;
            _lastAnimatedMenu = null;
        }

        private void BuildCombatantOptions(
            GameObject host,
            IReadOnlyList<ICombatant> values,
            ICombatant selected,
            Action<ICombatant> selectedAction)
        {
            if (host == null || values == null)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                ICombatant combatant = values[i];
                if (combatant == null)
                    continue;

                Button button = CreateOption(host.transform, GetCombatantLabel(combatant));
                if (button == null)
                    continue;

                button.interactable = !ReferenceEquals(combatant, selected);
                button.onClick.AddListener(() => selectedAction.Invoke(combatant));
            }
        }

        private void BuildHandoffOptions(GameObject host)
        {
            if (host == null || _session == null || _controller == null) return;

            foreach (ICombatant candidate in _session.Allies)
            {
                if (candidate == null || ReferenceEquals(candidate, _viewState.ActingActor)) continue;
                string name = UsesReferenceLayout ? GetCombatantLabel(candidate) :
                    _session.TryGetAllyCharacterId(candidate, out string characterId)
                        ? characterId : candidate.Id.Value.ToString();
                string reason = _controller.GetHandoffUnavailableReason(candidate);
                Button button = CreateOption(host.transform,
                    $"인계 {name}" + (reason == null ? string.Empty : $" · {reason}"));
                if (button == null) continue;
                button.interactable = reason == null &&
                    !ReferenceEquals(candidate, _viewState.SelectedHandoffTarget);
                if (reason == null)
                    button.onClick.AddListener(() => HandleHandoffTargetSelected(candidate));
            }
        }

        private void BuildSkillOptions(
            GameObject host,
            IReadOnlyList<ISkill> values,
            ISkill selected,
            Action<ISkill> selectedAction)
        {
            if (host == null || values == null)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                ISkill skill = values[i];
                if (skill == null)
                    continue;

                int mpCost = CombatMpCostResolver.Resolve(skill);
                Button button = CreateOption(host.transform, $"{skill.Name} MP {mpCost}");
                if (button == null)
                    continue;

                StyleCard(button, skill, "연계", ReferenceEquals(skill, selected), true);

                button.interactable = !ReferenceEquals(skill, selected);
                button.onClick.AddListener(() => selectedAction.Invoke(skill));
            }
        }

        private void BuildAvailabilityOptions(
            GameObject host,
            IReadOnlyList<CombatSkillAvailability> options,
            ISkill selected)
        {
            if (host == null || options == null)
                return;

            for (int i = 0; i < options.Count; i++)
            {
                CombatSkillAvailability option = options[i];
                ISkill skill = option.Skill;
                if (skill == null)
                    continue;

                string reason = GetUnavailableReason(option.Reason);
                string label = $"{skill.Name} MP {CombatMpCostResolver.Resolve(skill)}  " +
                               (option.IsAvailable ? "ON" : $"OFF ({reason})");
                Button button = CreateOption(host.transform, label);
                if (button == null)
                    continue;

                StyleCard(button, skill, "스킬", ReferenceEquals(skill, selected), option.IsAvailable, reason);

                ColorBlock colors = button.colors;
                if (!UsesReferenceLayout)
                    colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);
                button.colors = colors;
                button.interactable = option.IsAvailable && !ReferenceEquals(skill, selected);
                if (option.IsAvailable)
                    button.onClick.AddListener(() => HandleSkillSelected(skill));
            }
        }

        private void BuildHandOptions(GameObject host, PlayerCombatDecisionViewState state)
        {
            if (host == null) return;

            for (int i = 0; i < state.HandCards.Count; i++)
            {
                CombatHandCard card = state.HandCards[i];
                CombatSkillAvailability availability = i < state.CardAvailability.Count
                    ? state.CardAvailability[i]
                    : new CombatSkillAvailability(card.Skill, CombatSkillUnavailableReason.InvalidPhase);
                string kind = card.Kind == CombatCardKind.Clash ? "[합]" : "[일반]";
                bool selected = state.SelectedCardRuntimeId == card.RuntimeId;
                string label = $"{(selected ? "▶ 선택 · " : string.Empty)}{kind} {card.Skill.Name} · ID {card.RuntimeId}\n" +
                    $"MP {CombatMpCostResolver.Resolve(card.Skill)} · " +
                    (!availability.IsAvailable ? $"사용 불가: {GetUnavailableReason(availability.Reason)}" : selected ? "확정 전" : "사용 가능");
                Button button = CreateOption(host.transform, label);
                if (button == null) continue;
                StyleCard(button, card.Skill, card.Kind == CombatCardKind.Clash ? "합" : "일반",
                    selected, availability.IsAvailable, GetUnavailableReason(availability.Reason), card.RuntimeId);
                if (selected && !UsesReferenceLayout)
                {
                    ColorBlock colors = button.colors;
                    colors.disabledColor = new Color(0.25f, 0.65f, 0.85f, 1f);
                    button.colors = colors;
                }
                button.interactable = availability.IsAvailable &&
                    state.SelectedCardRuntimeId != card.RuntimeId;
                if (availability.IsAvailable)
                {
                    long runtimeId = card.RuntimeId;
                    button.onClick.AddListener(() => _controller?.SelectCard(runtimeId));
                }
            }
        }

        private void BuildBpInvestmentOptions(GameObject host)
        {
            if (host == null || _session?.BpRuntime == null ||
                _viewState.SelectedSkill == null || _controller == null) return;

            CombatBpRuntime bp = _session.BpRuntime;
            for (int amount = 0; amount <= bp.Config.MaximumInvestment; amount++)
            {
                string reason = _controller.GetBpInvestmentUnavailableReason(amount);
                bool available = reason == null;
                int magnitude = 0;
                CombatBpEnhancementKind kind = CombatBpEnhancementKind.None;
                if (amount > 0 && CombatBpEnhancementPolicy.TryGetProfile(_session,
                        _viewState.SelectedSkill, out kind, out _))
                    magnitude = CombatBpEnhancementPolicy.Magnitude(_session,
                        _viewState.SelectedSkill, amount, kind);
                string effect = amount == 0 ? "기본 효과" : DescribeBpEffect(kind, magnitude);
                string label = $"BP {amount} · {effect} " +
                    (available ? "ON" : $"OFF ({reason})");
                Button button = CreateOption(host.transform, label);
                if (button == null) continue;
                button.interactable = available && amount != _controller.SelectedBpInvestment;
                if (available)
                {
                    int selected = amount;
                    button.onClick.AddListener(() => _controller?.SelectBpInvestment(selected));
                }
            }
        }

        private static string DescribeBpEffect(CombatBpEnhancementKind kind, int magnitude)
        {
            return kind switch
            {
                CombatBpEnhancementKind.ClashPower => $"합 위력 +{magnitude}",
                CombatBpEnhancementKind.ExtraHits => $"추가 타격 +{Math.Min(31, magnitude)}",
                CombatBpEnhancementKind.Healing => $"회복 +{magnitude}",
                CombatBpEnhancementKind.StatusApplications =>
                    $"상태 적용 +{Math.Min(31, magnitude)}회",
                _ => "강화 없음"
            };
        }

        private static string GetUnavailableReason(CombatSkillUnavailableReason reason)
        {
            switch (reason)
            {
                case CombatSkillUnavailableReason.InsufficientMp: return "MP 부족";
                case CombatSkillUnavailableReason.InvalidPhase: return "현재 단계 사용 불가";
                case CombatSkillUnavailableReason.InvalidTarget: return "유효한 대상 없음";
                case CombatSkillUnavailableReason.RestrictedByStatus: return "상태 제한";
                case CombatSkillUnavailableReason.NotInHand: return "손패 없음";
                default: return string.Empty;
            }
        }

        private void BuildCombatItemOptions(
            GameObject host,
            IReadOnlyList<CombatItemOption> items,
            CombatItemOption selected)
        {
            if (host == null || items == null)
                return;

            if (UsesVideoDesign) UpdateItemArtwork(selected);

            for (int i = 0; i < items.Count; i++)
            {
                CombatItemOption item = items[i];
                if (item == null)
                    continue;

                Button button = CreateOption(host.transform, $"ITEM {item.DisplayName} x{item.Count}");
                if (button == null)
                    continue;

                bool isSelected = string.Equals(item.ItemId, selected?.ItemId, StringComparison.Ordinal);
                if (UsesReferenceLayout && isSelected)
                {
                    Text label = button.GetComponentInChildren<Text>(true);
                    if (label != null) label.text = $"▶ ITEM {item.DisplayName} x{item.Count}";
                    ColorBlock colors = button.colors;
                    colors.disabledColor = new Color(0.25f, 0.85f, 1f, 1f);
                    button.colors = colors;
                }
                if (UsesVideoDesign) StyleVideoItem(button, item, isSelected);
                button.interactable = !isSelected;
                button.onClick.AddListener(() => HandleCombatItemSelected(item));
            }
        }

        private void StyleCard(Button button, ISkill skill, string kind, bool selected, bool available,
            string reason = null, long? cardId = null)
        {
            if (!UsesReferenceLayout || cardFrameSprite == null) return;
            Image frame = button.image;
            Text title = button.GetComponentInChildren<Text>(true);
            if (frame == null || title == null) return;

            Color accent = selected ? new Color32(159, 224, 255, 255)
                : available ? new Color32(199, 212, 240, 255) : new Color32(149, 158, 174, 255);
            frame.sprite = null;
            frame.type = Image.Type.Simple;
            frame.color = selected ? new Color32(30, 104, 204, 255) : new Color32(11, 15, 24, 255);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            float width = UsesVideoDesign ? 184f : 142f;
            float height = UsesVideoDesign ? 254f : 154f;
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            if (Application.isPlaying)
            {
                GameObject slot = new GameObject("CardLayoutSlot", typeof(RectTransform), typeof(LayoutElement));
                RectTransform slotRect = (RectTransform)slot.transform;
                slotRect.SetParent(button.transform.parent, false);
                LayoutElement slotLayout = slot.GetComponent<LayoutElement>();
                slotLayout.minWidth = slotLayout.preferredWidth = width;
                slotLayout.minHeight = slotLayout.preferredHeight = height;
                RectTransform cardRect = (RectTransform)button.transform;
                cardRect.SetParent(slotRect, false);
                cardRect.anchorMin = Vector2.zero;
                cardRect.anchorMax = Vector2.one;
                cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
                float start = cardId.HasValue && _cardOffsets.TryGetValue(cardId.Value, out float offset) ? offset : 0f;
                float target = selected ? 8f : 0f;
                cardRect.anchoredPosition = Vector2.up * start;
                if (isActiveAndEnabled && start != target)
                    StartCoroutine(AnimateSelectedCard(cardRect, start, target, cardId));
            }

            Image face = CreateCardGraphic<Image>(frame.transform, "FilmFace", 0f, 1f, 14f);
            face.transform.SetAsFirstSibling();
            face.color = selected ? new Color32(15, 36, 69, 255) : new Color32(24, 30, 43, 255);
            if (UsesVideoDesign && FindSkillArtwork(skill) is Sprite artwork)
            {
                face.sprite = artwork;
                face.color = Color.white;
                face.preserveAspect = true;
            }
            Image footer = CreateCardGraphic<Image>(face.transform, "CostPlate", 0f, 0.37f, 0f);
            footer.color = new Color32(12, 19, 34, 255);

            title.name = "CardTitle";
            SetCardLabel(title, title.font, skill.Name, available || selected ? Color.white : accent, 23);
            PlaceCardGraphic(title.rectTransform, 0.37f, 0.82f, 20f);
            SetCardLabel(CreateCardGraphic<Text>(frame.transform, "CardKind", 0.82f, 1f, 18f),
                title.font, kind, accent, 15);
            string cost = $"MP {CombatMpCostResolver.Resolve(skill)}";
            if (selected && _session?.BpRuntime != null)
                cost += $"  /  BP {_controller.SelectedBpInvestment}";
            SetCardLabel(CreateCardGraphic<Text>(footer.transform, "CardCost", 0.50f, 1f, 4f),
                title.font, cost, Color.white, 17);
            string state = !available ? reason : selected ? "선택됨" : "사용 가능";
            SetCardLabel(CreateCardGraphic<Text>(footer.transform, "CardState", 0f, 0.50f, 4f),
                title.font, string.IsNullOrEmpty(state) ? "사용 불가" : state, accent, 15);

            int holeCount = UsesVideoDesign ? 22 : 7;
            for (int i = 0; i < holeCount; i++)
            {
                float y = (i + 0.5f) / holeCount;
                AddFilmPerforation(frame.transform, y, RectTransform.Edge.Left, accent);
                AddFilmPerforation(frame.transform, y, RectTransform.Edge.Right, accent);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = colors.disabledColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(0.8f, 0.9f, 1f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.9f);
            button.colors = colors;
            if (UsesVideoDesign)
            {
                frame.color = Color.black;
                if (face.sprite == null) face.color = new Color32(38, 38, 38, 255);
                footer.color = new Color32(0, 0, 0, 230);
                title.fontSize = title.resizeTextMaxSize = 25;
                title.rectTransform.anchorMin = new Vector2(0, .4f);
                title.rectTransform.anchorMax = new Vector2(1, .8f);
                foreach (Transform child in frame.transform)
                    if (child.name == "Perforation")
                    {
                        child.GetComponent<Image>().color = Color.white;
                        ((RectTransform)child).sizeDelta = new Vector2(7, 8);
                    }
                frame.transform.Find("CardKind").gameObject.SetActive(false);
                // The source illustrations are absent. Never assign unrelated skill art by index.
                Text costNumber = CreateCardGraphic<Text>(frame.transform, "FilmCostNumber", .73f, 1f, 0);
                SetCardLabel(costNumber, title.font, CombatMpCostResolver.Resolve(skill).ToString(), Color.white, 52);
                costNumber.rectTransform.anchorMin = costNumber.rectTransform.anchorMax = Vector2.up;
                costNumber.rectTransform.pivot = Vector2.one;
                costNumber.rectTransform.anchoredPosition = new Vector2(-3, 0);
                costNumber.rectTransform.sizeDelta = new Vector2(44, 70);
                colors.normalColor = colors.disabledColor = Color.black;
                colors.highlightedColor = colors.selectedColor = new Color32(12, 12, 12, 255);
                colors.pressedColor = new Color32(0, 61, 255, 255);
                button.colors = colors;
            }
        }

        private void StyleVideoItem(Button button, CombatItemOption item, bool selected)
        {
            button.image.color = Color.clear;
            Sprite source = itemPanelRoot.transform.Find("ItemRowSource")?.GetComponent<Image>()?.sprite;
            GameObject plate = source != null
                ? new GameObject("DiagonalItemPlate", typeof(RectTransform), typeof(Image))
                : new GameObject("DiagonalItemPlate", typeof(RectTransform), typeof(CombatReferenceGraphic));
            plate.transform.SetParent(button.transform, false);
            Graphic graphic = plate.GetComponent<Graphic>();
            PlaceCardGraphic(graphic.rectTransform, 0f, 1f, 0f);
            if (graphic is Image image) image.sprite = source;
            else ((CombatReferenceGraphic)graphic).Configure(CombatReferenceGraphic.Geometry.SlantedPlate, new Color32(0, 61, 255, 255), skew: 24f);
            graphic.raycastTarget = true;
            plate.transform.SetAsFirstSibling();
            Text label = button.GetComponentInChildren<Text>(true);
            label.text = item.DisplayName;
            label.fontSize = label.resizeTextMaxSize = 47;
            label.alignment = TextAnchor.MiddleLeft;
            PlaceCardGraphic(label.rectTransform, 0, 1, 110);
            label.rectTransform.offsetMax = new Vector2(-180, 0);
            Text quantity = CreateCardGraphic<Text>(button.transform, "ItemQuantity", 0, 1, 0);
            SetCardLabel(quantity, label.font, $"x{item.Count:000}", Color.black, 37);
            quantity.rectTransform.anchorMin = new Vector2(.8f, .15f);
            quantity.rectTransform.anchorMax = new Vector2(.97f, .85f);
            CombatReferenceGraphic capsule = CreateCardGraphic<CombatReferenceGraphic>(button.transform, "QuantityPlate", .15f, .85f, 0);
            capsule.rectTransform.anchorMin = new Vector2(.8f, .15f);
            capsule.rectTransform.anchorMax = new Vector2(.97f, .85f);
            capsule.Configure(CombatReferenceGraphic.Geometry.Capsule, Color.white);
            capsule.transform.SetSiblingIndex(quantity.transform.GetSiblingIndex());
            Text marker = CreateCardGraphic<Text>(button.transform, "ItemMarker", 0, 1, 0);
            SetCardLabel(marker, label.font, selected ? "●" : "○", Color.white, 65);
            marker.rectTransform.anchorMax = new Vector2(.12f, 1);
            CanvasGroup group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = selected ? 1f : .7f;
            ColorBlock colors = button.colors;
            colors.normalColor = colors.disabledColor = colors.selectedColor =
                colors.highlightedColor = colors.pressedColor = Color.clear;
            button.colors = colors;
        }

        private Sprite FindSkillArtwork(ISkill skill)
        {
            if (skill == null || skillArtwork == null) return null;
            foreach (SkillArtwork entry in skillArtwork)
                if (entry != null && entry.skillId == skill.Id.Value) return entry.artwork;
            return null;
        }

        private void UpdateItemArtwork(CombatItemOption selected)
        {
            Transform existing = itemPanelRoot.transform.Find("SelectedItemArtwork");
            Sprite sprite = null;
            if (selected != null && itemArtwork != null)
                foreach (ItemArtwork entry in itemArtwork)
                    if (entry != null && string.Equals(entry.itemId, selected.ItemId, StringComparison.Ordinal))
                    { sprite = entry.artwork; break; }
            if (existing == null && sprite == null) return;
            Image view;
            if (existing == null)
            {
                GameObject child = new("SelectedItemArtwork", typeof(RectTransform), typeof(Image));
                child.transform.SetParent(itemPanelRoot.transform, false);
                view = child.GetComponent<Image>();
                view.raycastTarget = false;
                view.preserveAspect = true;
                RectTransform rect = view.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(360, -40);
                rect.sizeDelta = new Vector2(460, 460);
                rect.localEulerAngles = new Vector3(0, 0, -30);
                child.transform.SetSiblingIndex(itemPanelRoot.transform.Find("ItemReel").GetSiblingIndex());
            }
            else view = existing.GetComponent<Image>();
            view.sprite = sprite;
            view.gameObject.SetActive(sprite != null);
        }

        private IEnumerator AnimateSelectedCard(RectTransform card, float start, float target, long? cardId)
        {
            const float duration = 0.14f;
            for (float elapsed = 0f; elapsed < duration && card != null && card.gameObject.activeInHierarchy;
                 elapsed += Time.unscaledDeltaTime)
            {
                float position = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                card.anchoredPosition = Vector2.up * position;
                if (cardId.HasValue) _cardOffsets[cardId.Value] = position;
                yield return null;
            }
            if (card != null && card.gameObject.activeInHierarchy)
            {
                card.anchoredPosition = Vector2.up * target;
                if (cardId.HasValue) _cardOffsets[cardId.Value] = target;
            }
        }

        private static T CreateCardGraphic<T>(Transform parent, string name, float bottom, float top,
            float inset) where T : Graphic
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            T graphic = child.AddComponent<T>();
            graphic.raycastTarget = false;
            PlaceCardGraphic(graphic.rectTransform, bottom, top, inset);
            return graphic;
        }

        private static void PlaceCardGraphic(RectTransform rect, float bottom, float top, float inset)
        {
            rect.anchorMin = new Vector2(0, bottom);
            rect.anchorMax = new Vector2(1, top);
            rect.offsetMin = new Vector2(inset, 0);
            rect.offsetMax = new Vector2(-inset, 0);
        }

        private static void SetCardLabel(Text text, Font font, string value, Color color, int size)
        {
            text.font = font;
            text.text = value;
            text.color = color;
            text.fontSize = text.resizeTextMaxSize = size;
            text.resizeTextMinSize = 12;
            text.resizeTextForBestFit = true;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
        }

        private static void AddFilmPerforation(Transform frame, float y, RectTransform.Edge edge, Color color)
        {
            Image hole = CreateCardGraphic<Image>(frame, "Perforation", y, y, 0f);
            hole.rectTransform.sizeDelta = new Vector2(0f, 6f);
            hole.rectTransform.SetInsetAndSizeFromParentEdge(edge, 4f, 6f);
            hole.color = color;
        }

        private void SetMenuButton(Button button, bool visible, bool available, bool selected)
        {
            if (button == null) return;
            SetVisible(button.gameObject, visible);
            button.interactable = available;
            if (button.targetGraphic != null)
                button.targetGraphic.color = UsesVideoDesign ? new Color32(0, 61, 255, 255) : selected ? new Color32(60, 129, 255, 255)
                    : available ? new Color32(25, 48, 95, 255) : new Color32(25, 34, 53, 255);
            if (UsesVideoDesign)
            {
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null) label.color = new Color(1, 1, 1, !available ? .45f : selected ? 1f : .8f);
                if (label != null)
                {
                    label.rectTransform.pivot = new Vector2(.5f, .5f);
                    label.rectTransform.anchoredPosition = button == actMenuButton ? new Vector2(-176, 94) :
                        button == skillMenuButton ? new Vector2(-198, 0) : new Vector2(-172, -94);
                    label.rectTransform.localScale = new Vector3(.85f, 1.6f, 1f);
                }
                button.targetGraphic.rectTransform.localScale = new Vector3(1, .76f, 1) * (selected ? 1.04f : 1f);
            }
            Image marker = button.transform.Find("MenuWedge")?.GetComponent<Image>();
            if (marker != null)
                marker.color = selected ? Color.white : new Color(0.65f, 0.76f, 1f, 0.65f);
        }

        private Button CreateOption(Transform parent, string label)
        {
            if (optionButtonPrefab == null || parent == null)
                return null;

            Button button = Instantiate(optionButtonPrefab, parent);
            _spawnedButtons.Add(button);
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                if (displayFont != null) text.font = displayFont;
                text.text = label;
                text.fontSize = 20;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 15;
                text.resizeTextMaxSize = 20;
                text.raycastTarget = false;
            }
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 250f;
            layout.preferredHeight = 64f;
            if (UsesReferenceLayout)
            {
                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                    image.color = new Color32(25, 48, 95, 255);
                }
                if (text != null) text.color = Color.white;
                if (parent == itemListRoot.transform)
                    layout.minHeight = layout.preferredHeight = 72f;
                if (UsesVideoDesign && (parent == actorListRoot.transform || parent == targetListRoot.transform || parent == handoffListRoot.transform))
                    StyleVideoChoice(button);
            }
            return button;
        }

        private static void StyleVideoChoice(Button button)
        {
            button.GetComponent<Image>().color = Color.clear;
            GameObject plate = new("ReferenceChoice", typeof(RectTransform), typeof(CombatReferenceGraphic));
            plate.transform.SetParent(button.transform, false);
            CombatReferenceGraphic graphic = plate.GetComponent<CombatReferenceGraphic>();
            PlaceCardGraphic(graphic.rectTransform, 0, 1, 0);
            graphic.Configure(CombatReferenceGraphic.Geometry.SlantedPlate, new Color32(0, 61, 255, 255));
            plate.transform.SetAsFirstSibling();
            button.targetGraphic = graphic;
            ColorBlock colors = button.colors;
            colors.normalColor = colors.selectedColor = new Color32(0, 61, 255, 255);
            colors.highlightedColor = new Color32(44, 92, 255, 255);
            colors.pressedColor = new Color32(0, 35, 180, 255);
            colors.disabledColor = new Color32(10, 20, 70, 255);
            button.colors = colors;
        }

        private void BuildUltimateOption(GameObject host)
        {
            ICombatant actor = _controller?.UltimateActor;
            int stage = 0;
            _session?.UltimateRuntime?.TryGetStage(actor, out stage);
            string reason = _controller?.UltimateUnavailableReason;
            Button button = CreateOption(host.transform,
                $"궁극기 {stage}/3" + (reason == null ? " 사용" : $" · {reason}"));
            if (button == null) return;
            button.interactable = reason == null;
            if (reason == null) button.onClick.AddListener(HandleUltimate);
        }

        private void BuildAllOutOption(GameObject host)
        {
            if (host == null)
                return;

            CombatExchangeDecisionRequest request = _orchestrator.PendingDecisionRequest;
            Button button = CreateOption(host.transform, "총공격 수락");
            if (button != null)
            {
                button.interactable = _viewState.CanAllOut;
                button.onClick.AddListener(() => _controller?.ConfirmAllOut(request));
            }
            Button decline = CreateOption(host.transform, "총공격 거절");
            decline?.onClick.AddListener(() => _controller?.DeclineAllOut(request));
        }

        private void BuildOvercomeOption(GameObject host)
        {
            if (host == null)
                return;

            Button button = CreateOption(host.transform, "극복");
            button?.onClick.AddListener(HandleOvercome);
        }

        private void ClearDynamicButtons()
        {
            for (int i = _spawnedButtons.Count - 1; i >= 0; i--)
            {
                Button button = _spawnedButtons[i];
                if (button != null)
                {
                    button.interactable = false;
                    if (_animationReady && !_itemsRequested && itemPanelRoot.activeSelf &&
                        button.transform.parent == itemListRoot.transform)
                    {
                        _closingItemButtons.Add(button);
                        continue;
                    }
                    button.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        GameObject target = button.transform.parent != null &&
                            button.transform.parent.name == "CardLayoutSlot"
                                ? button.transform.parent.gameObject : button.gameObject;
                        target.SetActive(false);
                        Destroy(target);
                    }
                    else
                        DestroyImmediate(button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }

        private void ClearClosingItems()
        {
            foreach (Button button in _closingItemButtons)
            {
                if (button == null) continue;
                button.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(button.gameObject);
                else DestroyImmediate(button.gameObject);
            }
            _closingItemButtons.Clear();
        }

        private void HandleActorSelected(ICombatant actor)
        {
            _menuSection = MenuSection.Skill;
            _controller?.SelectActor(actor);
        }

        private void HandleSkillSelected(ISkill skill)
        {
            if (_viewState.DecisionKind == CombatExchangeDecisionKind.Chain)
                _controller?.SelectHandoffSkill(skill);
            else
                _controller?.SelectSkill(skill);
        }

        private void HandleCombatItemSelected(CombatItemOption item)
        {
            _controller?.SelectCombatItem(item);
        }

        private void HandleTargetSelected(ICombatant target)
        {
            _controller?.SelectTarget(target);
        }

        private void HandleHandoffTargetSelected(ICombatant target)
        {
            _controller?.SelectHandoffTarget(target);
        }

        private void BindStaticButtons()
        {
            if (_staticButtonsBound)
                return;

            confirmButton?.onClick.AddListener(HandleConfirm);
            cancelButton?.onClick.AddListener(HandleCancel);
            noResponseButton?.onClick.AddListener(HandleNoResponse);
            continueButton?.onClick.AddListener(HandleContinue);
            handoffButton?.onClick.AddListener(HandleHandoff);
            endButton?.onClick.AddListener(HandleEnd);
            actMenuButton?.onClick.AddListener(HandleActMenu);
            skillMenuButton?.onClick.AddListener(HandleSkillMenu);
            itemsMenuButton?.onClick.AddListener(HandleItemsMenu);
            _staticButtonsBound = true;
        }

        private void UnbindStaticButtons()
        {
            if (!_staticButtonsBound)
                return;

            confirmButton?.onClick.RemoveListener(HandleConfirm);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            noResponseButton?.onClick.RemoveListener(HandleNoResponse);
            continueButton?.onClick.RemoveListener(HandleContinue);
            handoffButton?.onClick.RemoveListener(HandleHandoff);
            endButton?.onClick.RemoveListener(HandleEnd);
            actMenuButton?.onClick.RemoveListener(HandleActMenu);
            skillMenuButton?.onClick.RemoveListener(HandleSkillMenu);
            itemsMenuButton?.onClick.RemoveListener(HandleItemsMenu);
            _staticButtonsBound = false;
        }

        private void HandleConfirm() => _controller?.Confirm();
        private void HandleCancel()
        {
            _menuSection = MenuSection.Skill;
            _controller?.CancelSelection();
        }
        private void HandleActMenu() { _menuSection = MenuSection.Act; Render(); }
        private void HandleSkillMenu() { _menuSection = MenuSection.Skill; Render(); }
        private void HandleItemsMenu() { _menuSection = MenuSection.Items; Render(); }
        private void HandleNoResponse() => _controller?.ConfirmNoResponse();
        private void HandleContinue() => _controller?.ConfirmContinue();
        private void HandleHandoff() => _controller?.ConfirmHandoff();
        private void HandleEnd() => _controller?.EndChain();
        private void HandleAllOut() => _controller?.ConfirmAllOut();
        private void HandleOvercome() => _controller?.ConfirmOvercome();
        private void HandleUltimate() => _controller?.ConfirmUltimate();

        private void RefreshStatus()
        {
            if (!IsBound)
                return;

            Phase phase = _stateMachine != null ? _stateMachine.Phase : Phase.EnterCombat;
            SetText(phaseText, $"{_session.CardExchangeIndex}합 · {GetPhaseTitle(phase, _viewState.DecisionKind)}");
            string context = GetContextMessage(phase, _viewState,
                _orchestrator?.ClashSeriesDecisionTimeRemaining ?? 0f);
            if (itemPanelRoot != null && (_animationReady ? _itemsRequested : itemPanelRoot.activeSelf))
                context = _viewState.SelectedItem == null ? "사용할 아이템을 선택하세요."
                    : _viewState.SelectedTarget == null ? "아이템을 사용할 대상을 선택하세요."
                    : "아이템과 대상을 확인하세요.";
            if (_controller.IsAllOutCandidate)
                context = $"적 전원 기절 · 총공격 수락 / 거절\n남은 시간 {_orchestrator.AllOutDecisionTimeRemaining:0.0}초" +
                    (_controller.AllOutUnavailableReason is string reason ? $"\n선택 불가: {reason}" : string.Empty);
            string details = string.Empty;
            CombatClashSeriesState series = _session.ExchangeState.ClashSeries;
            if (!string.IsNullOrEmpty(_cardCommitFeedback) && phase != Phase.ExitCombat)
                details += $"\n{_cardCommitFeedback}";
            if (!string.IsNullOrEmpty(_lastComparisonFeedback) && phase == Phase.Clash)
                details += $"\n{_lastComparisonFeedback}";
            if (!string.IsNullOrEmpty(_lastClashFeedback) && phase != Phase.ExitCombat)
                details += $"\n{_lastClashFeedback}";
            if (_showExchangeFeedback && phase != Phase.ExitCombat && _session.HasHandValidation)
                details += $"\n{_displayedExchangeIndex}합 시작 · 기존 카드 사용 상태 초기화";
            if (phase == Phase.Clash && series != null)
            {
                details += $"\n강인도 {series.AttackerDurability} : {series.ResponderDurability}";
            }
            ICombatant player = _viewState.SelectedHandoffTarget ??
                (_viewState.ActingActor?.Side == Side.Allies ? _viewState.ActingActor : null) ??
                _viewState.SelectedActor ?? FindFirst(_session.Allies);
            ICombatant enemy = FindStatusEnemy();
            _lastPlayerHp = player?.HP ?? -1;
            _lastEnemyHp = enemy?.HP ?? -1;
            string playerName = UsesReferenceLayout ? GetCombatantLabel(player, Side.Allies) :
                _session.TryGetAllyCharacterId(player, out string characterId) ? characterId : "PLAYER";
            string enemyName = UsesReferenceLayout ? GetCombatantLabel(enemy, Side.Enemies) : "ENEMY";
            details += $"\n{BuildHandStatus(_session, player)}";
            if (_session.BpRuntime is CombatBpRuntime bp &&
                bp.TryGetBalance(_viewState.ActingActor ?? player, out int currentBp))
                details += $"\nBP 획득까지 {bp.RoundCyclesUntilGain}주기" +
                    (_viewState.SelectedSkill != null
                        ? $" · 투자 {_controller?.SelectedBpInvestment ?? 0}" : string.Empty);
            SetText(contextText, combatDetailsText != null ? context : context + details);
            SetText(combatDetailsText, details.TrimStart('\n'));
            SetText(playerHpText, FormatHp(playerName, player));
            SetText(enemyHpText, FormatHp(enemyName, enemy));
            SetText(playerMpText, FormatResources(UsesReferenceLayout ? null : playerName, player));
            SetText(enemyMpText, FormatResources(UsesReferenceLayout ? null : enemyName, enemy));
            float pressure = _session.StandoffState != null ? _session.StandoffState.CurrentPressure : 0f;
            float pressureMax = _session.StandoffState != null ? _session.StandoffState.MaxPressure : 0f;
            float pressurePercent = pressureMax > 0f ? pressure / pressureMax * 100f : 0f;
            SetText(pressureText, $"압박 {pressurePercent:0}%");
            SetText(playerPostureText, FormatPosture(UsesReferenceLayout ? null : playerName, player));
            SetText(enemyPostureText, FormatPosture(UsesReferenceLayout ? null : enemyName, enemy));
            if (UsesVideoDesign)
            {
                Text exchange = panelRoot.transform.Find("ExchangeMedallion/ExchangeNumber")?.GetComponent<Text>();
                if (exchange != null) exchange.text = _session.CardExchangeIndex.ToString();
                UpdateVideoHealth(playerHpText, player);
                UpdateVideoHealth(enemyHpText, enemy);
            }
            statusListPresenter?.Render(_session);
        }

        private static void UpdateVideoHealth(Text label, ICombatant combatant)
        {
            if (label == null || combatant == null) return;
            RectTransform fill = label.transform.parent.Find("HealthFill") as RectTransform;
            if (fill != null) fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                474f * (combatant.MaxHP > 0 ? Mathf.Clamp01((float)combatant.HP / combatant.MaxHP) : 0f));
        }

        private void ObserveCombatFeedback()
        {
            if (!IsBound)
                return;

            bool changed = false;
            TrackClashSeries();

            if (_session.CardExchangeIndex != _displayedExchangeIndex)
            {
                _displayedExchangeIndex = _session.CardExchangeIndex;
                _cardCommitFeedback = null;
                _shownCommittedCards.Clear();
                _commitFeedbackExchange = _displayedExchangeIndex;
                _showExchangeFeedback = true;
                changed = true;
            }
            else if (_showExchangeFeedback && _stateMachine.Phase == Phase.Clash)
            {
                _showExchangeFeedback = false;
                changed = true;
            }
            if (changed)
                RefreshStatus();
        }

        private void HandleCardCommitted(ICombatant actor, CombatHandCard card)
        {
            if (!IsBound || _stateMachine.Phase == Phase.ExitCombat || actor?.Side != Side.Allies || card == null)
                return;
            ObserveCombatFeedback();
            if (_commitFeedbackExchange != _session.CardExchangeIndex)
            {
                _commitFeedbackExchange = _session.CardExchangeIndex;
                _shownCommittedCards.Clear();
            }
            if (!_shownCommittedCards.Add(card.RuntimeId)) return;
            PresentCommittedCard(card);
            _cardCommitFeedback = $"사용 확정 · {card.Skill.Name} #{card.RuntimeId}";
            RefreshStatus();
        }

        private void TrackClashSeries()
        {
            if (_stateMachine?.Phase == Phase.ExitCombat) return;
            CombatClashSeriesState series = _session?.ExchangeState.ClashSeries;
            if (series == null || ReferenceEquals(series, _feedbackSeries)) return;
            _feedbackSeries = series;
            _lastFeedbackComparison = 0;
            _lastComparisonFeedback = null;
            _lastClashFeedback = null;
        }

        private void HandleClashFeedback(CombatClashSeriesState series, CombatClashRoundResult result)
        {
            if (!IsBound || _stateMachine.Phase == Phase.ExitCombat || series == null ||
                !ReferenceEquals(series, _session.ExchangeState.ClashSeries))
                return;
            TrackClashSeries();
            ICombatant attacker = _session.ExchangeState.CurrentDeclaration?.Attacker;
            bool changed = false;
            if (result != null && result.ComparisonNumber > _lastFeedbackComparison)
            {
                _lastFeedbackComparison = result.ComparisonNumber;
                string comparison = FormatClashOutcome(result.ComparisonOutcome, attacker)
                    .Replace("Clash 최종 결과 · ", string.Empty);
                _lastComparisonFeedback = $"비교 {result.ComparisonNumber} · 위력 {result.AttackerPower}:{result.ResponderPower} · {comparison}";
                changed = true;
            }
            if (series.IsComplete && series.FinalOutcome.HasValue && _lastClashFeedback == null)
            {
                _lastClashFeedback = FormatClashOutcome(series.FinalOutcome.Value, attacker);
                changed = true;
            }
            if (changed) RefreshStatus();
        }

        private static string FormatClashOutcome(CombatClashOutcome outcome, ICombatant attacker)
        {
            if (outcome == CombatClashOutcome.Tie)
                return "Clash 최종 결과 · 무승부";
            if (attacker == null)
                return "Clash 최종 결과 · 진영 정보 없음";
            if (outcome == CombatClashOutcome.Unopposed)
                return attacker.Side == Side.Allies
                    ? "Clash 최종 결과 · 승리 (무대응)" : "Clash 최종 결과 · 패배 (무대응)";
            bool playerWon = (outcome == CombatClashOutcome.AttackerWin) == (attacker.Side == Side.Allies);
            return playerWon ? "Clash 최종 결과 · 승리" : "Clash 최종 결과 · 패배";
        }

        // Presentation reads the session-owned hand only; it never infers card membership from skills.
        private static string BuildHandStatus(CombatSession session, ICombatant player)
        {
            if (session == null || !session.HasHandValidation)
                return "손패 시스템 비활성";

            if (player == null || !session.TryGetHand(player, out CombatHandRuntime hand))
                return "플레이어 손패 없음";

            int normalCount = 0;
            int clashCount = 0;
            for (int i = 0; i < hand.Hand.Count; i++)
            {
                if (hand.Hand[i].Kind == CombatCardKind.Clash)
                    clashCount++;
                else
                    normalCount++;
            }

            string used = FormatUsedCards(hand.NormalDiscard, hand.ClashDiscard);
            return $"미사용 일반 {normalCount} / 합 {clashCount} · " +
                $"이번 합 사용 {hand.NormalDiscard.Count + hand.ClashDiscard.Count}" +
                (used.Length > 0 ? $" ({used})" : string.Empty);
        }

        private static string FormatUsedCards(IReadOnlyList<CombatHandCard> normal, IReadOnlyList<CombatHandCard> clash)
        {
            List<string> names = new List<string>();
            foreach (CombatHandCard card in normal)
                names.Add($"일반 {card.Skill.Name} #{card.RuntimeId}");
            foreach (CombatHandCard card in clash)
                names.Add($"합 {card.Skill.Name} #{card.RuntimeId}");
            return string.Join(", ", names);
        }

        private string FormatResources(string label, ICombatant actor)
        {
            string value = FormatMp(label, actor);
            if (_session.BpRuntime != null &&
                _session.BpRuntime.TryGetBalance(actor, out int bp))
                value += $"  BP {bp}/{_session.BpRuntime.Config.MaximumBp}";
            if (_session.UltimateRuntime != null &&
                _session.UltimateRuntime.TryGetStage(actor, out int stage))
                value += $"  궁극기 {stage}/3 [{new string('=', stage)}{new string('-', 3 - stage)}]";
            return value;
        }

        private ICombatant FindStatusEnemy()
        {
            if (_viewState.ActingActor != null && _viewState.ActingActor.Side == Side.Enemies)
                return _viewState.ActingActor;
            if (UsesReferenceLayout && _viewState.SelectedTarget?.Side == Side.Enemies)
                return _viewState.SelectedTarget;
            return FindFirst(_session.Enemies);
        }

        private string GetCombatantLabel(ICombatant actor, Side emptySide = Side.Allies)
        {
            if (!UsesReferenceLayout)
                return actor?.Id.Value.ToString();
            Side side = actor?.Side ?? emptySide;
            IReadOnlyList<ICombatant> roster = side == Side.Allies ? _session.Allies : _session.Enemies;
            string role = side == Side.Enemies ? "적" : roster.Count > 1 ? "아군" : "플레이어";
            if (actor != null && roster.Count > 1)
                for (int i = 0; i < roster.Count; i++)
                    if (ReferenceEquals(roster[i], actor))
                        return $"{role} {i + 1}";
            return role;
        }

        private static string StatPrefix(string label) => label == null ? string.Empty : $"{label}  ";

        private string FormatMp(string label, ICombatant actor)
        {
            return _session.TryGetCombatState(actor, out CombatantCombatState state)
                ? $"{StatPrefix(label)}MP {state.CurrentMp} / {state.MaxMp}"
                : $"{StatPrefix(label)}MP -";
        }

        private static string FormatHp(string label, ICombatant actor)
        {
            return actor != null
                ? $"{label}  HP {actor.HP} / {actor.MaxHP}"
                : $"{label}  HP -";
        }

        private string FormatPosture(string label, ICombatant actor)
        {
            return _session.TryGetCombatState(actor, out CombatantCombatState state)
                ? $"{StatPrefix(label)}자세 {state.CurrentPosture} / {state.MaxPosture}" +
                  $"{(actor.IsStunned ? "  기절" : string.Empty)}  정신력 {state.CurrentMental} / {state.MaxMental}" +
                  $"{(state.IsPanicked ? "  패닉" : string.Empty)}"
                : $"{StatPrefix(label)}자세 -";
        }

        private static bool IsAutomaticPresentationPhase(Phase phase)
        {
            return phase == Phase.Approach || phase == Phase.Clash || phase == Phase.ApplyOutcome;
        }

        private static string GetPhaseTitle(Phase phase, CombatExchangeDecisionKind? decisionKind)
        {
            if (phase == Phase.ExitCombat)
                return "전투 종료";
            if (decisionKind == CombatExchangeDecisionKind.Response)
                return "대응 선택";
            if (decisionKind == CombatExchangeDecisionKind.ClashAttack ||
                decisionKind == CombatExchangeDecisionKind.ClashResponse)
                return "다음 비교 스킬 선택";
            if (decisionKind == CombatExchangeDecisionKind.Chain || phase == Phase.ChainDecision)
                return "연계 선택";

            switch (phase)
            {
                case Phase.Standoff:
                    return "대치";
                case Phase.AttackDeclaration:
                    return "공격 준비";
                case Phase.Approach:
                    return "접근";
                case Phase.Clash:
                    return "합 진행";
                case Phase.ApplyOutcome:
                    return "공격 결과";
                default:
                    return "전투 진행";
            }
        }

        private string GetContextMessage(
            Phase phase, PlayerCombatDecisionViewState viewState, float remainingSeconds)
        {
            if (phase == Phase.ExitCombat)
                return "전투가 종료되었습니다.";
            if (viewState?.ClashSeriesToken.HasValue == true)
                return $"다음 비교에 사용할 스킬을 선택하세요. 남은 시간 {remainingSeconds:0.0}초" +
                       (viewState.DecisionKind == CombatExchangeDecisionKind.ClashResponse
                           ? " · 대응할 수 없으면 무대응을 선택하세요." : string.Empty);
            if (phase == Phase.Approach)
                return "접근 중...";
            if (phase == Phase.Clash)
                return "합 판정 중...";
            if (phase == Phase.ApplyOutcome)
                return "공격 결과를 처리하고 있습니다.";

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Response)
            {
                CombatAttackDeclaration attack = viewState.DeclaredAttack;
                string declaration = viewState.DeclarationToken?.Revision > 0 && attack != null
                    ? $"공격 변경: {attack.Skill.Name} → {GetCombatantLabel(attack.Target)}. 이전 대응 선택이 해제되었습니다. · "
                    : string.Empty;
                if (viewState.SelectedSkill == null)
                    return declaration + "대응 스킬을 선택하거나 대응하지 않기를 선택하세요.";
                if (viewState.SelectedTarget == null)
                    return declaration + "대응할 대상을 선택하세요.";
                return declaration + "선택한 대응을 확정하세요.";
            }

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Chain)
            {
                if (!viewState.CanContinue && viewState.HandoffCandidates.Count == 0)
                    return "사용 가능한 연계가 없습니다. 연계 종료를 선택하세요.";
                if (viewState.SelectedHandoffTarget == null)
                    return "공격권을 유지했습니다. 다음 행동을 선택하세요.";
                if (viewState.SelectedSkill == null)
                    return "동료가 사용할 스킬을 선택하세요.";
                return "동료에게 넘길 연계를 확정하세요.";
            }

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Attack)
            {
                if (viewState.SkillAvailability.Count > 0 && viewState.SelectableSkills.Count == 0)
                    return "사용 가능한 스킬이 없습니다. 대치 중 MP가 회복되면 다시 선택할 수 있습니다.";
                if (viewState.CanOvercome)
                    return "패닉 상태입니다. 극복을 선택하세요.";
                if (viewState.SelectedActor == null)
                    return "행동 캐릭터를 선택하세요.";
                if (viewState.SelectedSkill == null)
                    return "사용할 스킬을 선택하세요.";
                if (viewState.SelectedTarget == null)
                    return "공격할 대상을 선택하세요.";
                return "선택한 공격을 확정하세요.";
            }

            return phase == Phase.Standoff
                ? "공격을 준비하세요. 대치 중에는 MP가 회복되며 압박이 상승합니다."
                : "적이 행동을 결정하고 있습니다...";
        }

        private static ICombatant FindFirst(IReadOnlyList<ICombatant> values)
        {
            if (values == null)
                return null;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != null)
                    return values[i];
            }

            return null;
        }

        private static void SetCommandButton(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.gameObject.SetActive(enabled);
            button.interactable = enabled;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible)
                target.SetActive(visible);
        }

        private void SetListVisible(GameObject list, bool visible)
        {
            SetVisible(list, visible);
            if (UsesReferenceLayout && list != null && list.transform.parent != null &&
                list.transform.parent.TryGetComponent(out ScrollRect scroll) && scroll.content == list.transform)
                SetVisible(scroll.gameObject, visible);
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private void ApplyDisplayFont()
        {
            if (displayFont == null)
                return;

            ApplyFont(phaseText);
            ApplyFont(playerHpText);
            ApplyFont(enemyHpText);
            ApplyFont(playerMpText);
            ApplyFont(enemyMpText);
            ApplyFont(pressureText);
            ApplyFont(playerPostureText);
            ApplyFont(enemyPostureText);
            ApplyButtonFont(confirmButton);
            ApplyButtonFont(cancelButton);
            ApplyButtonFont(noResponseButton);
            ApplyButtonFont(continueButton);
            ApplyButtonFont(handoffButton);
            ApplyButtonFont(endButton);
        }

        private void ApplyFont(Text target)
        {
            if (target != null)
                target.font = displayFont;
        }

        private void ApplyButtonFont(Button button)
        {
            if (button == null)
                return;

            ApplyFont(button.GetComponentInChildren<Text>(true));
        }

        private sealed class SessionHandoffEligibilityProvider : ICombatHandoffEligibilityProvider
        {
            public bool IsEligible(CombatSession session, ICombatant currentAttackOwner,
                ICombatant receivingActor)
            {
                return session?.IsHandoffEligible(receivingActor) == true;
            }
        }
    }
}
