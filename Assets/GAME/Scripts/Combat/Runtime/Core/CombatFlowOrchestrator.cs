// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatFlowOrchestrator.cs
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatFlowOrchestrator : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;

        private CombatSession _session;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        public void BindSession(CombatSession session)
        {
            _session = session;
        }

        public bool SubmitPlayerDraftAndAdvance(
            CombatPlanDraft draft,
            ICombatant playerActor,
            out string errorMessage)
        {
            errorMessage = null;

            if (_session == null)
            {
                errorMessage = "전투 세션이 연결되지 않았습니다.";
                return false;
            }

            if (entryPoint == null)
            {
                errorMessage = "CombatEntryPoint 참조가 없습니다.";
                return false;
            }

            if (!CombatPlanValidator.ValidatePlayerDraft(_session, draft, playerActor, out errorMessage))
                return false;

            CommitDraftToSession(draft);
            FillEnemyPlansFallbackIfMissing();
            CombatTurnResolver.ResolveTurn(_session);
            entryPoint.ConfirmPlanningFromUI();

            return true;
        }

        private void CommitDraftToSession(CombatPlanDraft draft)
        {
            foreach (var pair in draft.Plans)
            {
                _session.CurrentTurn.SetPlan(pair.Key, pair.Value);
            }
        }

        private void FillEnemyPlansFallbackIfMissing()
        {
            if (_session == null || _session.Allies.Count == 0)
                return;

            ICombatant player = _session.Allies[0];
            if (player == null || player.HP <= 0)
            {
                FillEnemiesWithNone();
                return;
            }

            foreach (var enemy in _session.Enemies)
            {
                if (enemy == null) continue;
                if (_session.CurrentTurn.TryGetPlan(enemy.Id, out _)) continue;

                if (enemy.HP <= 0 || enemy.IsStunned)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                int skillCount = Mathf.Min(enemy.Skills.Count, 3);
                ISkill skill = skillCount > 0
                    ? enemy.Skills[Mathf.Max(0, _session.TurnIndex - 1) % skillCount]
                    : null;

                if (skill == null)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                var pa = new PlannedAction(
                    skillId: skill.Id,
                    tag: skill.Tag,
                    targeting: skill.Targeting,
                    targetCombatantId: player.Id,
                    plannedSpeed: skill.Speed,
                    consumesTurn: skill.ConsumesTurn
                );

                _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(pa, PlannedAction.None));
            }
        }

        private void FillEnemiesWithNone()
        {
            if (_session == null || _session.CurrentTurn == null)
                return;

            foreach (ICombatant enemy in _session.Enemies)
            {
                if (enemy == null)
                    continue;

                if (!_session.CurrentTurn.TryGetPlan(enemy.Id, out _))
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
            }
        }
    }
}
