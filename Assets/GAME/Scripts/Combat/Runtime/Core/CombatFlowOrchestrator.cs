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
            if (draft == null || _session == null || _session.CurrentTurn == null)
                return;

            foreach (var pair in draft.Plans)
                _session.CurrentTurn.SetPlan(pair.Key, pair.Value);
        }

        private void FillEnemyPlansFallbackIfMissing()
        {
            if (_session == null || _session.CurrentTurn == null || _session.Allies.Count == 0)
                return;

            ICombatant player = _session.Allies[0];
            if (player == null || player.HP <= 0)
            {
                FillEnemiesWithNone();
                return;
            }

            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy == null)
                    continue;

                if (_session.CurrentTurn.TryGetPlan(enemy.Id, out _))
                    continue;

                if (enemy.HP <= 0 || enemy.IsStunned || enemy.Skills == null || enemy.Skills.Count == 0)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                int skillIndex = _session.TurnIndex % enemy.Skills.Count;
                ISkill selectedSkill = enemy.Skills[skillIndex];

                if (selectedSkill == null)
                {
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
                    continue;
                }

                PlannedAction enemyAction = new PlannedAction(
                    skillId: selectedSkill.Id,
                    tag: selectedSkill.Tag,
                    targeting: selectedSkill.Targeting,
                    targetCombatantId: player.Id,
                    plannedSpeed: selectedSkill.Speed,
                    consumesTurn: selectedSkill.ConsumesTurn
                );

                _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(enemyAction, PlannedAction.None));
            }
        }

        private void FillEnemiesWithNone()
        {
            if (_session == null || _session.CurrentTurn == null)
                return;

            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy == null)
                    continue;

                if (!_session.CurrentTurn.TryGetPlan(enemy.Id, out _))
                    _session.CurrentTurn.SetPlan(enemy.Id, new ActionPlan(PlannedAction.None, PlannedAction.None));
            }
        }
    }
}
