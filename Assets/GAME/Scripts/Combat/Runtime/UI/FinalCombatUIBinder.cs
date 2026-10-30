using System;
using System.Collections.Generic;
using Game.Combat.Core;
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

        private readonly List<Button> _spawnedButtons = new List<Button>();
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
        private int _lastAllOutWiringErrorVersion = -1;

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
            _controller = new FinalCombatPlayerCommandController(new SessionHandoffEligibilityProvider());
            _controller.ViewStateChanged += HandleViewStateChanged;
            _stateMachine.OnPhaseChanged += HandlePhaseChanged;
            _session.ExchangeState.OnStateChanged += HandleExchangeStateChanged;
            if (!_controller.Bind(orchestrator, session))
            {
                Unbind();
                return;
            }

            _viewState = _controller.ViewState;
            SetVisible(panelRoot, true);
            Render();
        }

        public void Unbind()
        {
            if (_stateMachine != null)
                _stateMachine.OnPhaseChanged -= HandlePhaseChanged;
            if (_session != null)
                _session.ExchangeState.OnStateChanged -= HandleExchangeStateChanged;
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
            _lastAllOutWiringErrorVersion = -1;
            _viewState = PlayerCombatDecisionViewState.Empty;
            ClearDynamicButtons();
            statusListPresenter?.Clear();
            SetVisible(panelRoot, false);
        }

        public void ApplyPhase(Phase phase)
        {
            if (!IsBound)
                return;

            RefreshStatus();
            Render();
        }

        private void HandlePhaseChanged(Phase previous, Phase next)
        {
            ApplyPhase(next);
            if (previous == Phase.Clash || next == Phase.Clash)
                TraceClashUi("PhaseChanged");
        }

        private void HandleViewStateChanged(PlayerCombatDecisionViewState state)
        {
            _viewState = state ?? PlayerCombatDecisionViewState.Empty;
            Render();
            if (_stateMachine?.Phase == Phase.Clash)
                TraceClashUi("ViewStateChanged");
        }

        private void HandleExchangeStateChanged(int version)
        {
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
            bool hasRequest = IsBound && _viewState.DecisionKind.HasValue &&
                              (!automaticPhase || phase == Phase.Clash && series);
            bool attack = _viewState.DecisionKind == CombatExchangeDecisionKind.Attack;
            bool response = _viewState.DecisionKind == CombatExchangeDecisionKind.Response;
            bool chain = _viewState.DecisionKind == CombatExchangeDecisionKind.Chain;

            ClearDynamicButtons();
            bool showUltimate = hasRequest && (attack || chain) &&
                _session?.UltimateRuntime != null;
            SetVisible(actorListRoot, hasRequest && attack &&
                (_viewState.SelectableActors.Count > 0 || showUltimate));
            SetVisible(skillListRoot, hasRequest && (attack || response || chain || series) &&
                (_viewState.HandCards.Count > 0 || _viewState.SkillAvailability.Count > 0 ||
                 _viewState.SelectableSkills.Count > 0));
            SetVisible(targetListRoot, hasRequest && (attack || response) && _viewState.SelectableTargets.Count > 0);
            bool allOut = hasRequest && chain && _controller.IsAllOutCandidate;
            SetVisible(handoffListRoot, hasRequest &&
                (chain && (_viewState.HandoffCandidates.Count > 0 || allOut) ||
                 attack && (_viewState.SelectableItems.Count > 0 || _viewState.CanOvercome ||
                            _viewState.SelectedSkill != null && _session?.BpRuntime != null) ||
                 response && _viewState.SelectedSkill != null && _session?.BpRuntime != null ||
                 series && _viewState.SelectedSkill != null && _session?.BpRuntime != null ||
                 showUltimate && chain));

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
            if (hasRequest && attack)
                BuildCombatItemOptions(handoffListRoot, _viewState.SelectableItems, _viewState.SelectedItem);
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

                Button button = CreateOption(host.transform, combatant.Id.Value.ToString());
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
                string name = _session.TryGetAllyCharacterId(candidate, out string characterId)
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

                ColorBlock colors = button.colors;
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
                string label = $"{kind} {card.Skill.Name} · ID {card.RuntimeId} · " +
                    $"MP {CombatMpCostResolver.Resolve(card.Skill)} " +
                    (availability.IsAvailable ? "ON" : $"OFF ({GetUnavailableReason(availability.Reason)})");
                Button button = CreateOption(host.transform, label);
                if (button == null) continue;
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

            ICombatant actor = _viewState.ActingActor ?? _viewState.SelectedActor;
            CombatBpRuntime bp = _session.BpRuntime;
            for (int amount = 0; amount <= bp.Config.MaximumInvestment; amount++)
            {
                bool available = _orchestrator.CanInvestBp(actor, _viewState.SelectedSkill, amount);
                int magnitude = 0;
                CombatBpEnhancementKind kind = CombatBpEnhancementKind.None;
                if (amount > 0 && CombatBpEnhancementPolicy.TryGetProfile(_session,
                        _viewState.SelectedSkill, out kind, out _))
                    magnitude = CombatBpEnhancementPolicy.Magnitude(_session,
                        _viewState.SelectedSkill, amount, kind);
                string effect = amount == 0 ? "기본 효과" : DescribeBpEffect(kind, magnitude);
                string label = $"BP {amount} · {effect} " +
                    (available ? "ON" : "OFF (BP 부족 또는 강화 없음)");
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

            for (int i = 0; i < items.Count; i++)
            {
                CombatItemOption item = items[i];
                if (item == null)
                    continue;

                Button button = CreateOption(host.transform, $"ITEM {item.DisplayName} x{item.Count}");
                if (button == null)
                    continue;

                button.interactable = !string.Equals(item.ItemId, selected?.ItemId, StringComparison.Ordinal);
                button.onClick.AddListener(() => HandleCombatItemSelected(item));
            }
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
                text.text = label;
                text.fontSize = 20;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 15;
                text.resizeTextMaxSize = 20;
            }
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 250f;
            layout.preferredHeight = 64f;
            return button;
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
                    button.gameObject.SetActive(false);
                    if (Application.isPlaying)
                        Destroy(button.gameObject);
                    else
                        DestroyImmediate(button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }

        private void HandleActorSelected(ICombatant actor)
        {
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
            _staticButtonsBound = false;
        }

        private void HandleConfirm() => _controller?.Confirm();
        private void HandleCancel() => _controller?.CancelSelection();
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
            SetText(phaseText, GetPhaseTitle(phase, _viewState.DecisionKind));
            string context = GetContextMessage(phase, _viewState,
                _orchestrator?.ClashSeriesDecisionTimeRemaining ?? 0f);
            if (_controller.IsAllOutCandidate)
                context = $"적 전원 기절 · 총공격 수락 / 거절\n남은 시간 {_orchestrator.AllOutDecisionTimeRemaining:0.0}초" +
                    (_controller.AllOutUnavailableReason is string reason ? $"\n선택 불가: {reason}" : string.Empty);
            CombatClashSeriesState series = _session.ExchangeState.ClashSeries;
            if (phase == Phase.Clash && series != null)
            {
                CombatClashRoundResult round = _orchestrator?.PendingClashRoundPresentationRequest?.Result;
                context += $"\n합 {series.ComparisonCount}: 공격측 강인도 {series.AttackerDurability}, " +
                           $"대응측 강인도 {series.ResponderDurability}";
                if (round != null)
                    context += $"\n합 위력 {round.AttackerPower} : {round.ResponderPower}";
            }
            ICombatant player = _viewState.SelectedHandoffTarget ??
                (_viewState.ActingActor?.Side == Side.Allies ? _viewState.ActingActor : null) ??
                _viewState.SelectedActor ?? FindFirst(_session.Allies);
            ICombatant enemy = FindStatusEnemy();
            string playerName = _session.TryGetAllyCharacterId(player, out string characterId)
                ? characterId : "PLAYER";
            context += $"\n{BuildHandStatus(_session, player)}";
            if (_session.BpRuntime is CombatBpRuntime bp &&
                bp.TryGetBalance(_viewState.ActingActor ?? player, out int currentBp))
                context += $"\nBP 획득까지 {bp.RoundCyclesUntilGain}주기" +
                    (_viewState.SelectedSkill != null
                        ? $" · 투자 {_controller?.SelectedBpInvestment ?? 0}" : string.Empty);
            SetText(contextText, context);
            SetText(playerHpText, FormatHp(playerName, player));
            SetText(enemyHpText, FormatHp("ENEMY", enemy));
            SetText(playerMpText, FormatResources(playerName, player));
            SetText(enemyMpText, FormatResources("ENEMY", enemy));
            float pressure = _session.StandoffState != null ? _session.StandoffState.CurrentPressure : 0f;
            float pressureMax = _session.StandoffState != null ? _session.StandoffState.MaxPressure : 0f;
            float pressurePercent = pressureMax > 0f ? pressure / pressureMax * 100f : 0f;
            SetText(pressureText, $"압박 {pressurePercent:0}%");
            SetText(playerPostureText, FormatPosture(playerName, player));
            SetText(enemyPostureText, FormatPosture("ENEMY", enemy));
            statusListPresenter?.Render(_session);
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

            return $"손패 일반 {normalCount} / 합 {clashCount} · " +
                $"Draw {hand.NormalDrawCount}+{hand.ClashDrawCount} · " +
                $"Discard {hand.NormalDiscard.Count}+{hand.ClashDiscard.Count} · " +
                $"Refill {hand.RefillCount}";
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
            return FindFirst(_session.Enemies);
        }

        private string FormatMp(string label, ICombatant actor)
        {
            return _session.TryGetCombatState(actor, out CombatantCombatState state)
                ? $"{label}  MP {state.CurrentMp} / {state.MaxMp}"
                : $"{label}  MP -";
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
                ? $"{label}  자세 {state.CurrentPosture} / {state.MaxPosture}" +
                  $"{(actor.IsStunned ? "  기절" : string.Empty)}  정신력 {state.CurrentMental} / {state.MaxMental}" +
                  $"{(state.IsPanicked ? "  패닉" : string.Empty)}"
                : $"{label}  자세 -";
        }

        private static bool IsAutomaticPresentationPhase(Phase phase)
        {
            return phase == Phase.Approach || phase == Phase.Clash || phase == Phase.ApplyOutcome;
        }

        private static string GetPhaseTitle(Phase phase, CombatExchangeDecisionKind? decisionKind)
        {
            if (decisionKind == CombatExchangeDecisionKind.Response)
                return "대응 선택";
            if (decisionKind == CombatExchangeDecisionKind.ClashAttack ||
                decisionKind == CombatExchangeDecisionKind.ClashResponse)
                return "반복 합 스킬 선택";
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

        private static string GetContextMessage(
            Phase phase, PlayerCombatDecisionViewState viewState, float remainingSeconds)
        {
            if (viewState?.ClashSeriesToken.HasValue == true)
                return $"다음 합 스킬을 선택하세요. 남은 시간 {remainingSeconds:0.0}초" +
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
                    ? $"공격 변경: {attack.Skill.Name} → {attack.Target.Id}. 이전 대응 선택이 해제되었습니다. · "
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
