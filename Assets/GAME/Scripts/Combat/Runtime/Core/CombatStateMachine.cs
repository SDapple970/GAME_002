// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatStateMachine.cs
using System;
using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CombatStateMachine
    {
        public Phase Phase { get; private set; } = Phase.EnterCombat;
        public CombatEndReason EndReason { get; private set; } = CombatEndReason.None;

        private readonly CombatSession _session;

        public event Action<CombatSession, Action> OnRequireResolutionPlay;
        public event Action<Phase, Phase> OnPhaseChanged;

        private bool _isResolving = false;

        public CombatStateMachine(CombatSession session)
        {
            _session = session;
        }

        public CombatStateMachine(CombatSession session, object legacyArg1, object legacyArg2)
        {
            _session = session;
        }

        public void ConfirmPlanning()
        {
            if (Phase == Phase.Planning)
            {
                Debug.Log("[CombatStateMachine] Planning -> Resolution");
                SetPhase(Phase.Resolution);
                _isResolving = false;
            }
        }

        public void ForceExit(CombatEndReason reason)
        {
            EndReason = reason;
            SetPhase(Phase.ExitCombat);
        }

        public void Tick()
        {
            switch (Phase)
            {
                case Phase.EnterCombat:
                    CheckCombatEndConditions();
                    if (Phase == Phase.ExitCombat)
                        break;

                    _session.BeginNewTurn();
                    SetPhase(Phase.Planning);
                    Debug.Log($"[CombatStateMachine] EnterCombat -> Planning. Turn={_session.TurnIndex}");
                    break;

                case Phase.Resolution:
                    if (!_isResolving)
                    {
                        _isResolving = true;

                        if (OnRequireResolutionPlay != null)
                        {
                            Debug.Log("[CombatStateMachine] Requesting resolution play.");
                            OnRequireResolutionPlay.Invoke(_session, OnResolutionFinished);
                        }
                        else
                        {
                            Debug.LogWarning("[CombatStateMachine] No CombatDirector bound. Skipping resolution animation.");
                            OnResolutionFinished();
                        }
                    }
                    break;

                case Phase.EndTurn:
                    EndTurn();
                    break;
            }
        }

        private void OnResolutionFinished()
        {
            SetPhase(Phase.EndTurn);
        }

        private void EndTurn()
        {
            CheckCombatEndConditions();
            if (Phase == Phase.ExitCombat)
                return;

            ClearStunsAtTurnEnd();
            _session.BeginNewTurn();
            _isResolving = false;
            SetPhase(Phase.Planning);
            Debug.Log($"[CombatStateMachine] EndTurn -> Planning. Turn={_session.TurnIndex}");
        }

        private void ClearStunsAtTurnEnd()
        {
            for (int i = 0; i < _session.Allies.Count; i++)
            {
                var combatant = _session.Allies[i];
                if (combatant != null && combatant.IsStunned)
                    StaggerSystem.ClearStunAtTurnEnd(combatant);
            }

            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                var combatant = _session.Enemies[i];
                if (combatant != null && combatant.IsStunned)
                    StaggerSystem.ClearStunAtTurnEnd(combatant);
            }
        }

        private void CheckCombatEndConditions()
        {
            var evaluated = CombatEndEvaluator.Evaluate(_session);
            if (evaluated == CombatEndReason.None)
                return;

            EndReason = evaluated;
            SetPhase(Phase.ExitCombat);
        }

        private void SetPhase(Phase next)
        {
            if (Phase == next)
                return;

            Phase previous = Phase;
            Phase = next;
            OnPhaseChanged?.Invoke(previous, next);
        }
    }
}
