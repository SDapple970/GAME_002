using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Model;
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

        private readonly List<Button> _spawnedButtons = new List<Button>();
        private FinalCombatPlayerCommandController _controller;
        private CombatSession _session;
        private CombatStateMachine _stateMachine;
        private PlayerCombatDecisionViewState _viewState = PlayerCombatDecisionViewState.Empty;
        private bool _staticButtonsBound;

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
            _controller = new FinalCombatPlayerCommandController();
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
            _viewState = PlayerCombatDecisionViewState.Empty;
            ClearDynamicButtons();
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
        }

        private void HandleViewStateChanged(PlayerCombatDecisionViewState state)
        {
            _viewState = state ?? PlayerCombatDecisionViewState.Empty;
            Render();
        }

        private void HandleExchangeStateChanged(int version)
        {
            RefreshStatus();
        }

        private void Render()
        {
            Phase phase = _stateMachine != null ? _stateMachine.Phase : Phase.EnterCombat;
            bool automaticPhase = IsAutomaticPresentationPhase(phase);
            bool hasRequest = IsBound && _viewState.DecisionKind.HasValue && !automaticPhase;
            bool attack = _viewState.DecisionKind == CombatExchangeDecisionKind.Attack;
            bool response = _viewState.DecisionKind == CombatExchangeDecisionKind.Response;
            bool chain = _viewState.DecisionKind == CombatExchangeDecisionKind.Chain;

            ClearDynamicButtons();
            SetVisible(actorListRoot, hasRequest && attack && _viewState.SelectableActors.Count > 0);
            SetVisible(skillListRoot, hasRequest && (attack || response || chain) &&
                (_viewState.SelectableSkills.Count > 0 || _viewState.SelectableItems.Count > 0 ||
                 _viewState.CanOvercome));
            SetVisible(targetListRoot, hasRequest && (attack || response) && _viewState.SelectableTargets.Count > 0);
            bool canAllOut = hasRequest && chain && _viewState.CanAllOut;
            SetVisible(handoffListRoot, hasRequest && chain &&
                (_viewState.HandoffCandidates.Count > 0 || canAllOut));

            if (hasRequest && attack)
                BuildCombatantOptions(actorListRoot, _viewState.SelectableActors, _viewState.SelectedActor, HandleActorSelected);
            if (hasRequest && (attack || response || chain))
                BuildSkillOptions(skillListRoot, _viewState.SelectableSkills, _viewState.SelectedSkill, HandleSkillSelected);
            if (hasRequest && attack)
                BuildCombatItemOptions(skillListRoot, _viewState.SelectableItems, _viewState.SelectedItem);
            if (hasRequest && attack && _viewState.CanOvercome)
                BuildOvercomeOption(skillListRoot);
            if (hasRequest && (attack || response))
                BuildCombatantOptions(targetListRoot, _viewState.SelectableTargets, _viewState.SelectedTarget, HandleTargetSelected);
            if (hasRequest && chain)
                BuildCombatantOptions(handoffListRoot, _viewState.HandoffCandidates, _viewState.SelectedHandoffTarget, HandleHandoffTargetSelected);

            if (canAllOut)
                BuildAllOutOption(handoffListRoot);

            SetCommandButton(confirmButton, hasRequest && (attack || response) && _viewState.CanConfirm);
            SetCommandButton(cancelButton, hasRequest && (attack || response || chain));
            SetCommandButton(noResponseButton, hasRequest && response && _viewState.CanNoResponse);
            SetCommandButton(continueButton, hasRequest && chain && _viewState.CanContinue);
            SetCommandButton(handoffButton, hasRequest && chain && _viewState.CanHandoff);
            SetCommandButton(endButton, hasRequest && chain && _viewState.CanEnd);
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
                text.text = label;
            return button;
        }

        private void BuildAllOutOption(GameObject host)
        {
            if (host == null)
                return;

            Button button = CreateOption(host.transform, "ALL-OUT");
            button?.onClick.AddListener(HandleAllOut);
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
                    Destroy(button.gameObject);
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

        private void RefreshStatus()
        {
            if (!IsBound)
                return;

            Phase phase = _stateMachine != null ? _stateMachine.Phase : Phase.EnterCombat;
            SetText(phaseText, GetPhaseTitle(phase, _viewState.DecisionKind));
            SetText(contextText, GetContextMessage(phase, _viewState));

            ICombatant player = FindFirst(_session.Allies);
            ICombatant enemy = FindStatusEnemy();
            SetText(playerHpText, FormatHp("PLAYER", player));
            SetText(enemyHpText, FormatHp("ENEMY", enemy));
            SetText(playerMpText, FormatMp("PLAYER", player));
            SetText(enemyMpText, FormatMp("ENEMY", enemy));
            float pressure = _session.StandoffState != null ? _session.StandoffState.CurrentPressure : 0f;
            float pressureMax = _session.StandoffState != null ? _session.StandoffState.MaxPressure : 0f;
            float pressurePercent = pressureMax > 0f ? pressure / pressureMax * 100f : 0f;
            SetText(pressureText, $"압박 {pressurePercent:0}%");
            SetText(playerPostureText, FormatPosture("PLAYER", player));
            SetText(enemyPostureText, FormatPosture("ENEMY", enemy));
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
                ? $"{label}  자세 {state.CurrentPosture} / {state.MaxPosture}{(actor.IsStunned ? "  기절" : string.Empty)}\n" +
                  $"Mental {state.CurrentMental} / {state.MaxMental}{(state.IsPanicked ? "  패닉" : string.Empty)}"
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

        private static string GetContextMessage(Phase phase, PlayerCombatDecisionViewState viewState)
        {
            if (phase == Phase.Approach)
                return "접근 중...";
            if (phase == Phase.Clash)
                return "합 판정 중...";
            if (phase == Phase.ApplyOutcome)
                return "공격 결과를 처리하고 있습니다.";

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Response)
            {
                if (viewState.SelectedSkill == null)
                    return "대응 스킬을 선택하거나 대응하지 않기를 선택하세요.";
                if (viewState.SelectedTarget == null)
                    return "대응할 대상을 선택하세요.";
                return "선택한 대응을 확정하세요.";
            }

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Chain)
            {
                if (viewState.SelectedHandoffTarget == null)
                    return "공격권을 유지했습니다. 다음 행동을 선택하세요.";
                if (viewState.SelectedSkill == null)
                    return "동료가 사용할 스킬을 선택하세요.";
                return "동료에게 넘길 연계를 확정하세요.";
            }

            if (viewState?.DecisionKind == CombatExchangeDecisionKind.Attack)
            {
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
    }
}
