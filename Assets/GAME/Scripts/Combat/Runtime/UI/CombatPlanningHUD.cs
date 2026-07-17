using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    public sealed class CombatPlanningHUD : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private CombatFlowOrchestrator flowOrchestrator;

        [Header("Panel")]
        [SerializeField] private GameObject panelPlanning;

        [Header("Roots")]
        [SerializeField] private RectTransform skillListRoot;
        [SerializeField] private RectTransform targetListRoot;

        [Header("Prefabs")]
        [SerializeField] private Button buttonPrefab;

        [Header("Confirm")]
        [SerializeField] private Button confirmButton;

        [Header("Text")]
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Text statusText;

        private CombatSession _session;
        private ICombatant _player;
        private ISkill _selectedSkill;
        private ICombatant _selectedTarget;
        private int _shownTurnIndex = -1;
        private CombatEntryPoint _subscribedEntryPoint;
        private CombatStateMachine _activeStateMachine;
        private bool _confirmBound;
        private bool _submittedThisPlanning;
        private bool _missingEntryPointWarned;
        private bool _missingPanelPlanningWarned;
        private bool _missingConfirmButtonWarned;

        private void Awake()
        {
            AutoBindReferences();
            WarnIfMissingBindings();
            Hide();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            WarnIfMissingBindings();
            SubscribeToEntryPoint();
            BindConfirmButton();
            RecoverActiveSessionIfNeeded();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
            UnsubscribeFromStateMachine();
            UnbindConfirmButton();
        }

        public void Bind(CombatSession session)
        {
            if (ReferenceEquals(_session, session))
                return;

            _session = session;
            _shownTurnIndex = -1;
            ResetSelection();
        }

        public void Show()
        {
            EnterPlanning(_session != null ? _session.CurrentTurn : null);
        }

        public void EnterPlanning(CombatTurn turn)
        {
            if (_session == null)
                return;

            if (_shownTurnIndex == _session.TurnIndex &&
                panelPlanning != null && panelPlanning.activeSelf)
            {
                RefreshConfirmState();
                return;
            }

            _player = _session.Allies.Count > 0 ? _session.Allies[0] : null;
            _shownTurnIndex = _session.TurnIndex;
            _submittedThisPlanning = false;
            ResetSelection();

            if (panelPlanning != null)
            {
                panelPlanning.SetActive(true);
            }
            else
            {
                WarnIfMissingPanelPlanning();
            }

            RebuildSkillButtons();
            RebuildTargetButtons();
            RefreshConfirmState();
        }

        public void Hide()
        {
            ExitPlanning();
        }

        public void ExitPlanning()
        {
            if (confirmButton != null)
                confirmButton.interactable = false;

            if (panelPlanning != null)
            {
                panelPlanning.SetActive(false);
            }
            else
            {
                WarnIfMissingPanelPlanning();
            }

            SetMessage(string.Empty);
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (flowOrchestrator == null)
                flowOrchestrator = FindFirstObjectByType<CombatFlowOrchestrator>();
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribedEntryPoint == entryPoint)
                return;

            UnsubscribeFromEntryPoint();
            _subscribedEntryPoint = entryPoint;
            if (_subscribedEntryPoint == null)
                return;

            _subscribedEntryPoint.OnCombatStarted += HandleCombatStarted;
            _subscribedEntryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (_subscribedEntryPoint != null)
            {
                _subscribedEntryPoint.OnCombatStarted -= HandleCombatStarted;
                _subscribedEntryPoint.OnCombatEnded -= HandleCombatEnded;
            }

            _subscribedEntryPoint = null;
        }

        private void SubscribeToStateMachine(CombatStateMachine stateMachine)
        {
            if (_activeStateMachine == stateMachine)
                return;

            UnsubscribeFromStateMachine();
            _activeStateMachine = stateMachine;
            if (_activeStateMachine != null)
                _activeStateMachine.OnPhaseChanged += HandlePhaseChanged;
        }

        private void UnsubscribeFromStateMachine()
        {
            if (_activeStateMachine != null)
                _activeStateMachine.OnPhaseChanged -= HandlePhaseChanged;

            _activeStateMachine = null;
        }

        private void BindConfirmButton()
        {
            if (_confirmBound)
                return;

            if (confirmButton == null)
            {
                WarnIfMissingConfirmButton();
                return;
            }

            confirmButton.onClick.AddListener(Confirm);
            _confirmBound = true;
        }

        private void UnbindConfirmButton()
        {
            if (!_confirmBound || confirmButton == null)
            {
                _confirmBound = false;
                return;
            }

            confirmButton.onClick.RemoveListener(Confirm);
            _confirmBound = false;
        }

        private void RecoverActiveSessionIfNeeded()
        {
            if (entryPoint == null || entryPoint.ActiveSession == null)
                return;

            Bind(entryPoint.ActiveSession);
            SubscribeToStateMachine(entryPoint.ActiveStateMachine);
            if (entryPoint.ActiveStateMachine != null)
                ApplyPhase(entryPoint.ActiveStateMachine.Phase);
        }

        private void HandleCombatStarted(CombatSession session)
        {
            Bind(session);
            SubscribeToStateMachine(entryPoint != null ? entryPoint.ActiveStateMachine : null);
            ApplyPhase(_activeStateMachine != null ? _activeStateMachine.Phase : Phase.EnterCombat);
        }

        private void HandlePhaseChanged(Phase previous, Phase next)
        {
            ApplyPhase(next);
        }

        private void ApplyPhase(Phase phase)
        {
            if (phase == Phase.Planning)
                EnterPlanning(_session != null ? _session.CurrentTurn : null);
            else
                ExitPlanning();
        }

        private void HandleCombatEnded(CombatResult result)
        {
            ExitPlanning();
            UnsubscribeFromStateMachine();
            _session = null;
            _player = null;
            _selectedSkill = null;
            _selectedTarget = null;
            _shownTurnIndex = -1;
        }

        private void WarnIfMissingBindings()
        {
            WarnIfMissingEntryPoint();
            WarnIfMissingPanelPlanning();
            WarnIfMissingConfirmButton();
        }

        private void WarnIfMissingEntryPoint()
        {
            if (entryPoint != null || _missingEntryPointWarned)
                return;

            _missingEntryPointWarned = true;
            Debug.LogWarning("[CombatPlanningHUD] CombatEntryPoint auto-bind failed. Assign EntryPoint in the Inspector.", this);
        }

        private void WarnIfMissingPanelPlanning()
        {
            if (panelPlanning != null || _missingPanelPlanningWarned)
                return;

            _missingPanelPlanningWarned = true;
            Debug.LogWarning("[CombatPlanningHUD] PanelPlanning is missing. Combat planning HUD visibility cannot be toggled.", this);
        }

        private void WarnIfMissingConfirmButton()
        {
            if (confirmButton != null || _missingConfirmButtonWarned)
                return;

            _missingConfirmButtonWarned = true;
            Debug.LogWarning("[CombatPlanningHUD] ConfirmButton is missing. The player cannot submit a combat plan from this HUD.", this);
        }

        private void RebuildSkillButtons()
        {
            ClearChildren(skillListRoot);

            if (_player == null)
            {
                SetMessage("No player combatant found in the active combat session.");
                return;
            }

            if (_player.Skills == null || _player.Skills.Count == 0)
            {
                Debug.LogWarning("[CombatPlanningHUD] Player has no combat skills.", this);
                SetMessage("No available skills. Check the player's CombatSkillLoadoutComponent and skill definitions.");
                return;
            }

            if (buttonPrefab == null)
            {
                Debug.LogWarning("[CombatPlanningHUD] ButtonPrefab is missing. Skill buttons cannot be created.", this);
                SetMessage("Skill list cannot be shown because ButtonPrefab is missing.");
                return;
            }

            if (skillListRoot == null)
            {
                Debug.LogWarning("[CombatPlanningHUD] SkillListRoot is missing. Skill buttons cannot be created.", this);
                SetMessage("Skill list cannot be shown because SkillListRoot is missing.");
                return;
            }

            int created = 0;
            int count = Mathf.Min(_player.Skills.Count, 3);
            for (int i = 0; i < count; i++)
            {
                ISkill skill = _player.Skills[i];
                if (skill == null)
                    continue;

                ISkill localSkill = skill;
                Button button = Instantiate(buttonPrefab, skillListRoot);
                SetButtonText(button, $"{localSkill.Name}  Cost:{localSkill.InspirationCost}");
                button.onClick.AddListener(() => SelectSkill(localSkill));
                created++;
            }

            if (created == 0)
                SetMessage("No usable skill buttons were created.");
        }

        private void RebuildTargetButtons()
        {
            ClearChildren(targetListRoot);

            if (_session == null)
            {
                SetMessage("No active combat session. Target list cannot be shown.");
                return;
            }

            if (buttonPrefab == null)
            {
                Debug.LogWarning("[CombatPlanningHUD] ButtonPrefab is missing. Target buttons cannot be created.", this);
                SetMessage("Target list cannot be shown because ButtonPrefab is missing.");
                return;
            }

            if (targetListRoot == null)
            {
                Debug.LogWarning("[CombatPlanningHUD] TargetListRoot is missing. Target buttons cannot be created.", this);
                SetMessage("Target list cannot be shown because TargetListRoot is missing.");
                return;
            }

            bool hasLivingEnemy = false;
            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy == null || enemy.HP <= 0)
                    continue;

                hasLivingEnemy = true;
                ICombatant localEnemy = enemy;
                Button button = Instantiate(buttonPrefab, targetListRoot);
                SetButtonText(button, $"Enemy {localEnemy.Id.Value}  HP {localEnemy.HP}/{localEnemy.MaxHP}");
                button.onClick.AddListener(() => SelectTarget(localEnemy));
            }

            if (!hasLivingEnemy)
                SetMessage("No living enemy targets are available.");
        }

        private void SelectSkill(ISkill skill)
        {
            _selectedSkill = skill;
            _selectedTarget = GetDefaultTargetFor(skill, _selectedTarget);

            if (RequiresEnemyTarget(skill) && _selectedTarget == null)
                SetMessage($"{skill.Name}: select a target.");
            else
                SetMessage($"{skill.Name} selected.");

            RefreshConfirmState();
        }

        private void SelectTarget(ICombatant target)
        {
            if (target == null || target.HP <= 0)
            {
                SetMessage("Selected target is not available.");
                return;
            }

            _selectedTarget = target;
            SetMessage(_selectedSkill != null ? $"{_selectedSkill.Name} -> Enemy {target.Id.Value}" : "Select a skill first.");
            RefreshConfirmState();
        }

        private void Confirm()
        {
            if (_submittedThisPlanning)
                return;

            if (!CanConfirm(out string reason))
            {
                SetMessage(reason);
                return;
            }

            CombatPlanDraft draft = new CombatPlanDraft();
            draft.SetSlot(_player.Id, 0, BuildAction(_selectedSkill, _selectedTarget));
            draft.SetSlot(_player.Id, 1, PlannedAction.None);

            if (flowOrchestrator == null)
            {
                SetMessage("CombatFlowOrchestrator is missing.");
                return;
            }

            if (!flowOrchestrator.SubmitPlayerDraftAndAdvance(draft, _player, out string errorMessage))
            {
                SetMessage(string.IsNullOrEmpty(errorMessage) ? "Confirm failed." : errorMessage);
                RefreshConfirmState();
                return;
            }

            _submittedThisPlanning = true;
            if (confirmButton != null)
                confirmButton.interactable = false;

            Hide();
        }

        private PlannedAction BuildAction(ISkill skill, ICombatant target)
        {
            return new PlannedAction(
                skillId: skill.Id,
                tag: skill.Tag,
                targeting: skill.Targeting,
                targetCombatantId: target != null ? target.Id : default,
                plannedSpeed: skill.Speed,
                consumesTurn: skill.ConsumesTurn
            );
        }

        private void RefreshConfirmState()
        {
            if (confirmButton == null)
                return;

            confirmButton.interactable = !_submittedThisPlanning && CanConfirm(out _);
        }

        private bool CanConfirm(out string reason)
        {
            reason = null;

            if (_session == null)
            {
                reason = "No active combat session.";
                return false;
            }

            if (_player == null || _player.HP <= 0)
            {
                reason = "No player combatant can act.";
                return false;
            }

            if (_selectedSkill == null)
            {
                reason = "Select a skill.";
                return false;
            }

            if (RequiresEnemyTarget(_selectedSkill) && (_selectedTarget == null || _selectedTarget.HP <= 0))
            {
                reason = "Select a living target.";
                return false;
            }

            return true;
        }

        private ICombatant GetDefaultTargetFor(ISkill skill, ICombatant currentTarget)
        {
            if (skill == null)
                return null;

            if (skill.Targeting == TargetingRule.Self)
                return _player;

            if (!RequiresEnemyTarget(skill))
                return null;

            if (currentTarget != null && currentTarget.HP > 0)
                return currentTarget;

            return null;
        }

        private static bool RequiresEnemyTarget(ISkill skill)
        {
            if (skill == null)
                return false;

            return skill.Targeting == TargetingRule.SingleEnemy ||
                   skill.Targeting == TargetingRule.AnySingle ||
                   skill.Targeting == TargetingRule.AllEnemies ||
                   skill.Tag == SkillTag.Inspect;
        }

        private void ResetSelection()
        {
            _selectedSkill = null;
            _selectedTarget = null;
        }

        private void SetMessage(string message)
        {
            if (errorText != null)
                errorText.text = message;

            if (statusText != null)
                statusText.text = message;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null)
                return;

            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = text;
                return;
            }

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
                legacyLabel.text = text;
        }
    }
}
