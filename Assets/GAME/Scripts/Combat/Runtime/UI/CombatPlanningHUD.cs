// GAME/Scripts/Combat/UI/CombatPlanningHUD.cs
using System.Collections.Generic;
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

        [Header("Panel")]
        [SerializeField] private GameObject panelPlanning;

        [Header("Roots")]
        [SerializeField] private RectTransform skillListRoot;
        [SerializeField] private RectTransform targetListRoot;

        [Header("Prefabs")]
        [SerializeField] private Button buttonPrefab;

        [Header("Slots")]
        [SerializeField] private Button slot1Button;
        [SerializeField] private Button slot2Button;

        [Header("Confirm")]
        [SerializeField] private Button confirmButton;

        [Header("Text (Legacy)")]
        [SerializeField] private Text statusText;

        [Header("MVP")]
        [SerializeField] private int actorAllyIndex = 0;
        [SerializeField] private bool autoFillEnemyPlansOnConfirm = true;
        
        [SerializeField] private CombatFlowOrchestrator flowOrchestrator;

        private CombatPlanDraft _draft = new CombatPlanDraft();


        private CombatSession _session;
        private ICombatant _actor;
        private int _selectedSlot;
        private ISkill _pendingSkill;
        private int _shownTurnIndex = -1;

        private readonly Dictionary<SkillId, string> _skillNameById = new();

        private void OnEnable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted += HandleCombatStarted;
                entryPoint.OnCombatEnded += HandleCombatEnded;
            }

            if (slot1Button != null)
                slot1Button.onClick.AddListener(OnSlot1Clicked);

            if (slot2Button != null)
                slot2Button.onClick.AddListener(OnSlot2Clicked);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;
            }

            if (slot1Button != null)
                slot1Button.onClick.RemoveListener(OnSlot1Clicked);

            if (slot2Button != null)
                slot2Button.onClick.RemoveListener(OnSlot2Clicked);

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Confirm);
        }

        private void Update()
        {
            if (_session == null || entryPoint == null || entryPoint.ActiveStateMachine == null)
                return;

            if (entryPoint.ActiveStateMachine.Phase == Phase.Planning &&
                _session.TurnIndex != _shownTurnIndex)
            {
                _shownTurnIndex = _session.TurnIndex;
                ShowPlanningUI();
            }
        }

        private void HandleCombatStarted(CombatSession session)
        {
            _session = session;
            _shownTurnIndex = -1;
            _draft.Clear();
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (panelPlanning != null)
                panelPlanning.SetActive(false);

            _session = null;
            _actor = null;
            _pendingSkill = null;
            _shownTurnIndex = -1;

            SetStatus("Combat ended.");
        }

        private void ShowPlanningUI()
        {
            if (_session == null)
                return;

            _actor = (_session.Allies.Count > actorAllyIndex)
                ? _session.Allies[actorAllyIndex]
                : null;

            _pendingSkill = null;
            _selectedSlot = 0;

            if (panelPlanning != null)
                panelPlanning.SetActive(true);

            CacheSkillNames();
            EnsureDraftExists(_actor);
            RebuildSkillButtons();
            RebuildTargetButtons();
            RefreshSlotLabels();

            SetStatus("슬롯 선택 → 스킬 선택 → (필요시) 타겟 선택");
        }

        private void CacheSkillNames()
        {
            _skillNameById.Clear();

            if (_actor == null)
                return;

            foreach (ISkill skill in _actor.Skills)
            {
                if (skill != null)
                    _skillNameById[skill.Id] = skill.Name;
            }
        }

        private void OnSlot1Clicked()
        {
            SelectSlot(0);
        }

        private void OnSlot2Clicked()
        {
            SelectSlot(1);
        }

        private void SelectSlot(int slot)
        {
            _selectedSlot = Mathf.Clamp(slot, 0, 1);
            SetStatus($"슬롯 {_selectedSlot + 1} 선택됨. 스킬을 고르세요.");
        }

        private void RebuildSkillButtons()
        {
            ClearChildren(skillListRoot);

            if (_actor == null || buttonPrefab == null || skillListRoot == null)
                return;

            foreach (ISkill skill in _actor.Skills)
            {
                if (skill == null)
                    continue;

                ISkill local = skill;
                Button btn = Instantiate(buttonPrefab, skillListRoot);
                SetButtonText(btn, $"{local.Name} (Tag:{local.Tag}, Cost:{local.InspirationCost})");
                btn.onClick.AddListener(() => OnSkillClicked(local));
            }
        }

        private void RebuildTargetButtons()
        {
            ClearChildren(targetListRoot);

            if (_session == null || buttonPrefab == null || targetListRoot == null)
                return;

            foreach (ICombatant enemy in _session.Enemies)
            {
                if (enemy == null)
                    continue;

                ICombatant local = enemy;
                Button btn = Instantiate(buttonPrefab, targetListRoot);

                string weakText = (_session.Knowledge != null &&
                                   _session.Knowledge.IsWeaknessRevealed(local.Id))
                    ? local.Weakness.ToString()
                    : "???";

                SetButtonText(btn, $"Enemy {local.Id}  HP:{local.HP}/{local.MaxHP}  Weak:{weakText}");
                btn.onClick.AddListener(() => OnTargetClicked(local));
            }
        }

        private void OnSkillClicked(ISkill skill)
        {
            _pendingSkill = null;

            if (RequiresTarget(skill))
            {
                _pendingSkill = skill;
                SetStatus($"[{skill.Name}] 선택됨. 타겟을 고르세요.");
                return;
            }

            CommitToSlot(skill, null);
        }

        private void OnTargetClicked(ICombatant target)
        {
            if (_pendingSkill == null)
            {
                SetStatus("타겟 선택됨. 먼저 스킬을 고르세요.");
                return;
            }

            CommitToSlot(_pendingSkill, target);
            _pendingSkill = null;
        }

        private static bool RequiresTarget(ISkill skill)
        {
            if (skill == null)
                return false;

            if (skill.Tag == SkillTag.Inspect)
                return true;

            if (skill.Targeting == TargetingRule.SingleEnemy)
                return true;

            return false;
        }

        private void CommitToSlot(ISkill skill, ICombatant targetOrNull)
        {
            if (_session == null || _actor == null || skill == null) return;

            if (RequiresTarget(skill) && targetOrNull == null)
            {
                SetStatus("이 스킬은 타겟이 필요합니다.");
                return;
            }

            var pa = new PlannedAction(
                skillId: skill.Id,
                tag: skill.Tag,
                targeting: skill.Targeting,
                targetCombatantId: targetOrNull != null ? targetOrNull.Id : default,
                plannedSpeed: skill.Speed,
                consumesTurn: skill.ConsumesTurn
            );

            _draft.SetSlot(_actor.Id, _selectedSlot, pa);

            RefreshSlotLabels();
            SetStatus($"슬롯 {_selectedSlot + 1} 예약 완료: {skill.Name}");
        }

        private void RefreshSlotLabels()
        {
            if (_actor == null) return;

            if (!_draft.TryGetPlan(_actor.Id, out var plan))
                plan = new ActionPlan(PlannedAction.None, PlannedAction.None);

            if (slot1Button != null) SetButtonText(slot1Button, SlotLabel(1, plan.Slot1));
            if (slot2Button != null) SetButtonText(slot2Button, SlotLabel(2, plan.Slot2));
        }

        private string SlotLabel(int slotNo, PlannedAction action)
        {
            if (action.IsNone)
                return $"Slot {slotNo}: (empty)";

            string name = _skillNameById.TryGetValue(action.SkillId, out string skillName)
                ? skillName
                : action.SkillId.ToString();

            string targetText = action.Targeting == TargetingRule.SingleEnemy
                ? $" -> {action.TargetCombatantId}"
                : string.Empty;

            return $"Slot {slotNo}: {name}{targetText}";
        }

        private void Confirm()
        {
            if (_session == null) return;
            if (flowOrchestrator == null)
            {
                SetStatus("FlowOrchestrator가 연결되지 않았습니다.");
                return;
            }

            if (!flowOrchestrator.SubmitPlayerDraftAndAdvance(_draft, _actor, out var error))
            {
                SetStatus(error);
                return;
            }

            if (panelPlanning != null) panelPlanning.SetActive(false);
            SetStatus("Confirmed.");
        }

        private void AutoFillEnemiesIfMissing()
        {
            if (_session == null || _session.Allies.Count == 0)
                return;

            ICombatant firstAlly = _session.Allies[0];

            foreach (ICombatant enemy in _session.Enemies)
            {
                if (enemy == null)
                    continue;

                if (_session.CurrentTurn.TryGetPlan(enemy.Id, out _))
                    continue;

                if (enemy.IsStunned)
                {
                    _session.CurrentTurn.SetPlan(
                        enemy.Id,
                        new ActionPlan(PlannedAction.None, PlannedAction.None)
                    );
                    continue;
                }

                ISkill skill = enemy.Skills.Count > 0 ? enemy.Skills[0] : null;
                if (skill == null)
                {
                    _session.CurrentTurn.SetPlan(
                        enemy.Id,
                        new ActionPlan(PlannedAction.None, PlannedAction.None)
                    );
                    continue;
                }

                PlannedAction plannedAction = new PlannedAction(
                    skillId: skill.Id,
                    tag: skill.Tag,
                    targeting: skill.Targeting,
                    targetCombatantId: firstAlly.Id,
                    plannedSpeed: skill.Speed,
                    consumesTurn: skill.ConsumesTurn
                );

                _session.CurrentTurn.SetPlan(
                    enemy.Id,
                    new ActionPlan(plannedAction, PlannedAction.None)
                );
            }
        }

        private void EnsureDraftExists(ICombatant c)
        {
            if (c == null) return;
            _draft.EnsureActor(c.Id);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null)
                return;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;
        }
    }
}