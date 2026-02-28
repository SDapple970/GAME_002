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
        [SerializeField] private int actorAllyIndex = 0; // 기본: 첫 아군
        [SerializeField] private bool autoFillEnemyPlansOnConfirm = true;

        private CombatSession _session;
        private ICombatant _actor;

        private int _selectedSlot = 0;      // 0=slot1, 1=slot2
        private ISkill _pendingSkill = null; // 타겟 선택 대기
        private int _shownTurnIndex = -1;

        private readonly Dictionary<SkillId, string> _skillNameById = new();


        private void HandleCombatEnded()
        {
            if (panelPlanning != null) panelPlanning.SetActive(false);

            _session = null;
            _actor = null;
            _pendingSkill = null;
            _shownTurnIndex = -1;

            SetStatus("Combat ended.");
        }

        private void OnEnable()
        {
            if (entryPoint != null)
                entryPoint.OnCombatStarted += HandleCombatStarted;

            if (slot1Button != null) slot1Button.onClick.AddListener(() => SelectSlot(0));
            if (slot2Button != null) slot2Button.onClick.AddListener(() => SelectSlot(1));
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

            entryPoint.OnCombatStarted += HandleCombatStarted;
            entryPoint.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void Update()
        {
            if (_session == null || entryPoint == null || entryPoint.ActiveStateMachine == null) return;

            if (entryPoint.ActiveStateMachine.Phase == Phase.Planning && _session.TurnIndex != _shownTurnIndex)
            {
                _shownTurnIndex = _session.TurnIndex;
                ShowPlanningUI();
            }
        }

        private void HandleCombatStarted(CombatSession session)
        {
            _session = session;
            _shownTurnIndex = -1;
        }

        private void ShowPlanningUI()
        {
            if (_session == null) return;

            _actor = (_session.Allies.Count > actorAllyIndex) ? _session.Allies[actorAllyIndex] : null;
            _pendingSkill = null;
            _selectedSlot = 0;

            if (panelPlanning != null) panelPlanning.SetActive(true);

            CacheSkillNames();
            EnsurePlanExists(_actor);

            RebuildSkillButtons();
            RebuildTargetButtons();
            RefreshSlotLabels();
            SetStatus("슬롯 선택 → 스킬 선택 → (필요시) 타겟 선택");
        }

        private void CacheSkillNames()
        {
            _skillNameById.Clear();
            if (_actor == null) return;
            foreach (var s in _actor.Skills)
                if (s != null) _skillNameById[s.Id] = s.Name;
        }

        private void SelectSlot(int slot)
        {
            _selectedSlot = Mathf.Clamp(slot, 0, 1);
            SetStatus($"슬롯 {_selectedSlot + 1} 선택됨. 스킬을 고르세요.");
        }

        private void RebuildSkillButtons()
        {
            ClearChildren(skillListRoot);
            if (_actor == null || buttonPrefab == null || skillListRoot == null) return;

            foreach (var s in _actor.Skills)
            {
                if (s == null) continue;
                var local = s;

                var btn = Instantiate(buttonPrefab, skillListRoot);
                SetButtonText(btn, $"{local.Name} (Tag:{local.Tag}, Cost:{local.InspirationCost})");
                btn.onClick.AddListener(() => OnSkillClicked(local));
            }
        }

        private void RebuildTargetButtons()
        {
            ClearChildren(targetListRoot);
            if (_session == null || buttonPrefab == null || targetListRoot == null) return;

            foreach (var enemy in _session.Enemies)
            {
                if (enemy == null) continue;
                var local = enemy;

                var btn = Instantiate(buttonPrefab, targetListRoot);

                string weakText = (_session.Knowledge != null && _session.Knowledge.IsWeaknessRevealed(local.Id))
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

            CommitToSlot(skill, targetOrNull: null);
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
            if (skill == null) return false;
            if (skill.Tag == SkillTag.Inspect) return true;
            if (skill.Targeting == TargetingRule.SingleEnemy) return true;
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

            if (!_session.CurrentTurn.TryGetPlan(_actor.Id, out var cur))
                cur = new ActionPlan(PlannedAction.None, PlannedAction.None);

            var pa = new PlannedAction(
                skillId: skill.Id,
                tag: skill.Tag,
                targeting: skill.Targeting,
                targetCombatantId: targetOrNull != null ? targetOrNull.Id : default,
                plannedSpeed: skill.Speed,
                consumesTurn: skill.ConsumesTurn
            );

            var s1 = cur.Slot1;
            var s2 = cur.Slot2;

            if (_selectedSlot == 0) s1 = pa;
            else s2 = pa;

            _session.CurrentTurn.SetPlan(_actor.Id, new ActionPlan(s1, s2));

            RefreshSlotLabels();
            SetStatus($"슬롯 {_selectedSlot + 1} 예약 완료: {skill.Name}");
        }

        private void RefreshSlotLabels()
        {
            if (_session == null || _actor == null) return;

            if (!_session.CurrentTurn.TryGetPlan(_actor.Id, out var plan))
                plan = new ActionPlan(PlannedAction.None, PlannedAction.None);

            if (slot1Button != null) SetButtonText(slot1Button, SlotLabel(1, plan.Slot1));
            if (slot2Button != null) SetButtonText(slot2Button, SlotLabel(2, plan.Slot2));
        }

        private string SlotLabel(int slotNo, PlannedAction a)
        {
            if (a.IsNone) return $"Slot {slotNo}: (empty)";

            string name = _skillNameById.TryGetValue(a.SkillId, out var n) ? n : a.SkillId.ToString();
            string tgt = (a.Targeting == TargetingRule.SingleEnemy) ? $" -> {a.TargetCombatantId}" : "";
            return $"Slot {slotNo}: {name}{tgt}";
        }

        private void Confirm()
        {
            if (_session == null) return;

            if (autoFillEnemyPlansOnConfirm)
                AutoFillEnemiesIfMissing();

            if (panelPlanning != null) panelPlanning.SetActive(false);

            // ✅ [NEW] Resolution 상태로 넘어가기 직전, 전투 결과를 전부 미리 계산하여 Playbook 대본을 생성합니다!
            CombatTurnResolver.ResolveTurn(_session);

            // StateMachine을 Planning -> Resolution 으로 넘김
            entryPoint.ConfirmPlanningFromUI();

            SetStatus("Confirmed.");
        }

        private void AutoFillEnemiesIfMissing()
        {
            if (_session.Allies.Count == 0) return;
            var firstAlly = _session.Allies[0];

            foreach (var enemy in _session.Enemies)
            {
                if (enemy == null) continue;
                if (_session.CurrentTurn.TryGetPlan(enemy.Id, out _)) continue;

                if (enemy.IsStunned)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                ISkill s = (enemy.Skills.Count > 0) ? enemy.Skills[0] : null;
                if (s == null)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                var pa = new PlannedAction(
                    skillId: s.Id,
                    tag: s.Tag,
                    targeting: s.Targeting,
                    targetCombatantId: firstAlly.Id,
                    plannedSpeed: s.Speed,
                    consumesTurn: s.ConsumesTurn
                );

                _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(pa, PlannedAction.None));
            }
        }

        private void EnsurePlanExists(ICombatant c)
        {
            if (_session == null || c == null) return;
            if (_session.CurrentTurn.TryGetPlan(c.Id, out _)) return;
            _session.CurrentTurn.SetPlan(c.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private static void SetButtonText(Button btn, string text)
        {
            if (btn == null) return;
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }
    }
}