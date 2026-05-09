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
                Phase = Phase.Resolution;
                _isResolving = false;
            }
        }

        public void ForceExit(CombatEndReason reason)
        {
            EndReason = reason;
            Phase = Phase.ExitCombat;
        }

        public void Tick()
        {
            switch (Phase)
            {
               
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
            }
        }

        private void OnResolutionFinished()
        {
            Phase = Phase.EndTurn;
        }

        private void CheckCombatEndConditions()
        {
            var evaluated = CombatEndEvaluator.Evaluate(_session);
            if (evaluated == CombatEndReason.None)
                return;

            EndReason = evaluated;
            Phase = Phase.ExitCombat;
        }
    }
}