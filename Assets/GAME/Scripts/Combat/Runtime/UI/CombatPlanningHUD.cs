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
        private bool _entrySubscribed;
        private bool _confirmBound;
        private bool _submittedThisPlanning;

        private void Awake()
        {
            AutoBindReferences();
            Hide();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToEntryPoint();
            BindConfirmButton();
            RecoverActiveSessionIfNeeded();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
            UnbindConfirmButton();
        }

        private void Update()
        {
            if (_session == null || entryPoint == null || entryPoint.ActiveStateMachine == null)
                return;

            if (entryPoint.ActiveStateMachine.Phase == Phase.Planning &&
                _session.TurnIndex != _shownTurnIndex)
            {
                Show();
            }
        }

        public void Bind(CombatSession session)
        {
            _session = session;
            _shownTurnIndex = -1;
            ResetSelection();
        }

        public void Show()
        {
            if (_session == null)
                return;

            _player = _session.Allies.Count > 0 ? _session.Allies[0] : null;
            _shownTurnIndex = _session.TurnIndex;
            _submittedThisPlanning = false;
            ResetSelection();

            if (panelPlanning != null)
                panelPlanning.SetActive(true);

            RebuildSkillButtons();
            RebuildTargetButtons();
            RefreshConfirmState();
            SetMessage(string.Empty);
        }

        public void Hide()
        {
            if (panelPlanning != null)
                panelPlanning.SetActive(false);

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
            if (_entrySubscribed || entryPoint == null)
                return;

            entryPoint.OnCombatStarted += HandleCombatStarted;
            entryPoint.OnCombatEnded += HandleCombatEnded;
            _entrySubscribed = true;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (!_entrySubscribed || entryPoint == null)
            {
                _entrySubscribed = false;
                return;
            }

            entryPoint.OnCombatStarted -= HandleCombatStarted;
            entryPoint.OnCombatEnded -= HandleCombatEnded;
            _entrySubscribed = false;
        }

        private void BindConfirmButton()
        {
            if (_confirmBound || confirmButton == null)
                return;

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
            if (entryPoint.ActiveStateMachine != null && entryPoint.ActiveStateMachine.Phase == Phase.Planning)
                Show();
        }

        private void HandleCombatStarted(CombatSession session)
        {
            Bind(session);
            Show();
        }

        private void HandleCombatEnded(CombatResult result)
        {
            Hide();
            _session = null;
            _player = null;
            _selectedSkill = null;
            _selectedTarget = null;
            _shownTurnIndex = -1;
        }

        private void RebuildSkillButtons()
        {
            ClearChildren(skillListRoot);

            if (_player == null)
            {
                SetMessage("플레이어 전투원을 찾지 못했습니다.");
                return;
            }

            if (_player.Skills.Count == 0)
            {
                Debug.LogWarning("[CombatPlanningHUD] Player has no combat skills.", this);
                SetMessage("사용 가능한 스킬이 없습니다.");
                return;
            }

            if (buttonPrefab == null || skillListRoot == null)
                return;

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
            }
        }

        private void RebuildTargetButtons()
        {
            ClearChildren(targetListRoot);

            if (_session == null || buttonPrefab == null || targetListRoot == null)
                return;

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
                SetMessage("선택 가능한 적이 없습니다.");
        }

        private void SelectSkill(ISkill skill)
        {
            _selectedSkill = skill;
            _selectedTarget = GetDefaultTargetFor(skill, _selectedTarget);

            if (RequiresEnemyTarget(skill) && _selectedTarget == null)
                SetMessage($"{skill.Name}: 대상을 선택하세요.");
            else
                SetMessage($"{skill.Name} 선택됨.");

            RefreshConfirmState();
        }

        private void SelectTarget(ICombatant target)
        {
            if (target == null || target.HP <= 0)
            {
                SetMessage("선택할 수 없는 대상입니다.");
                return;
            }

            _selectedTarget = target;
            SetMessage(_selectedSkill != null ? $"{_selectedSkill.Name} -> Enemy {target.Id.Value}" : "스킬을 먼저 선택하세요.");
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
                SetMessage("CombatFlowOrchestrator 참조가 없습니다.");
                return;
            }

            if (!flowOrchestrator.SubmitPlayerDraftAndAdvance(draft, _player, out string errorMessage))
            {
                SetMessage(string.IsNullOrEmpty(errorMessage) ? "Confirm 실패" : errorMessage);
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
                reason = "전투 세션이 없습니다.";
                return false;
            }

            if (_player == null || _player.HP <= 0)
            {
                reason = "행동 가능한 플레이어가 없습니다.";
                return false;
            }

            if (_selectedSkill == null)
            {
                reason = "스킬을 선택하세요.";
                return false;
            }

            if (RequiresEnemyTarget(_selectedSkill) && (_selectedTarget == null || _selectedTarget.HP <= 0))
            {
                reason = "살아있는 적 대상을 선택하세요.";
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

            if (_session == null)
                return null;

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
