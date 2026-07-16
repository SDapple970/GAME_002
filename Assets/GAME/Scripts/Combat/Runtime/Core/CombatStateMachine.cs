using System;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatStateMachine
    {
        public Phase Phase { get; private set; } = Phase.EnterCombat;
        public CombatEndReason EndReason { get; private set; } = CombatEndReason.None;

        private readonly CombatSession _session;
        private CombatTurn _presentingTurn;
        private int _presentingTurnIndex = -1;
        private bool _presentationRequested;
        private bool _endTurnProcessed;
        private bool _exited;

        public event Action<CombatSession, Action> OnRequireResolutionPlay;
        public event Action<Phase, Phase> OnPhaseChanged;

        public CombatStateMachine(CombatSession session)
        {
            _session = session;
        }

        public CombatStateMachine(CombatSession session, object legacyArg1, object legacyArg2)
        {
            _session = session;
        }

        public bool ConfirmPlanning()
        {
            if (_exited || Phase != Phase.Planning || _session?.CurrentTurn == null)
                return false;

            if (_session.CurrentTurn.Lifecycle != CombatTurnLifecycle.Resolved)
            {
                Debug.LogWarning(
                    $"[CombatStateMachine] Resolution entry rejected. Turn={_session.TurnIndex}, " +
                    $"Lifecycle={_session.CurrentTurn.Lifecycle}.");
                return false;
            }

            _presentationRequested = false;
            _endTurnProcessed = false;
            _presentingTurn = null;
            _presentingTurnIndex = -1;
            SetPhase(Phase.Resolution);
            return true;
        }

        public void ForceExit(CombatEndReason reason)
        {
            if (_exited)
                return;

            EnterExit(reason == CombatEndReason.None ? CombatEndReason.Abort : reason);
        }

        public void Tick()
        {
            if (_exited)
                return;

            switch (Phase)
            {
                case Phase.EnterCombat:
                    CombatEndReason initialEnd = CombatEndEvaluator.Evaluate(_session);
                    if (initialEnd != CombatEndReason.None)
                    {
                        EnterExit(initialEnd);
                        break;
                    }

                    if (_session == null || !_session.TryBeginNewTurn())
                    {
                        EnterExit(CombatEndReason.Abort);
                        break;
                    }

                    SetPhase(Phase.Planning);
                    Debug.Log($"[CombatStateMachine] EnterCombat -> Planning. Turn={_session.TurnIndex}");
                    break;

                case Phase.Resolution:
                    BeginPresentationOnce();
                    break;

                case Phase.EndTurn:
                    EndTurnOnce();
                    break;
            }
        }

        private void BeginPresentationOnce()
        {
            if (_presentationRequested || _session?.CurrentTurn == null)
                return;

            CombatTurn turn = _session.CurrentTurn;
            if (!turn.TryBeginPresentation())
            {
                Debug.LogError(
                    $"[CombatStateMachine] Presentation rejected. Turn={_session.TurnIndex}, Lifecycle={turn.Lifecycle}.");
                return;
            }

            _presentationRequested = true;
            _presentingTurn = turn;
            _presentingTurnIndex = _session.TurnIndex;
            int turnIndex = _presentingTurnIndex;
            Action completion = () => OnResolutionFinished(_session, turn, turnIndex);

            Action<CombatSession, Action> handler = GetSinglePresentationHandler();
            if (handler == null)
            {
                Debug.LogWarning("[CombatStateMachine] No CombatDirector bound. Completing resolution immediately.");
                completion();
                return;
            }

            try
            {
                handler.Invoke(_session, completion);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                completion();
            }
        }

        private Action<CombatSession, Action> GetSinglePresentationHandler()
        {
            if (OnRequireResolutionPlay == null)
                return null;

            Delegate[] handlers = OnRequireResolutionPlay.GetInvocationList();
            if (handlers.Length > 1)
                Debug.LogWarning("[CombatStateMachine] Multiple resolution presenters are bound. Only the first will run.");

            return handlers[0] as Action<CombatSession, Action>;
        }

        private void OnResolutionFinished(CombatSession expectedSession, CombatTurn expectedTurn, int expectedTurnIndex)
        {
            if (_exited || Phase != Phase.Resolution)
                return;

            if (!ReferenceEquals(_session, expectedSession) ||
                !ReferenceEquals(_session.CurrentTurn, expectedTurn) ||
                !ReferenceEquals(_presentingTurn, expectedTurn) ||
                _session.TurnIndex != expectedTurnIndex ||
                _presentingTurnIndex != expectedTurnIndex)
            {
                return;
            }

            if (!expectedTurn.TryMarkPresented())
                return;

            SetPhase(Phase.EndTurn);
        }

        private void EndTurnOnce()
        {
            if (_endTurnProcessed || _session?.CurrentTurn == null)
                return;

            CombatTurn completedTurn = _session.CurrentTurn;
            if (!completedTurn.TryComplete())
                return;

            _endTurnProcessed = true;

            CombatEndReason evaluated = CombatEndEvaluator.Evaluate(_session);
            if (evaluated != CombatEndReason.None)
            {
                EnterExit(evaluated);
                return;
            }

            ClearStunsAtTurnEnd();
            if (!_session.TryBeginNewTurn())
            {
                EnterExit(CombatEndReason.Abort);
                return;
            }

            _presentationRequested = false;
            _endTurnProcessed = false;
            _presentingTurn = null;
            _presentingTurnIndex = -1;
            SetPhase(Phase.Planning);
            Debug.Log($"[CombatStateMachine] EndTurn -> Planning. Turn={_session.TurnIndex}");
        }

        private void ClearStunsAtTurnEnd()
        {
            ClearStuns(_session.Allies);
            ClearStuns(_session.Enemies);
        }

        private static void ClearStuns(System.Collections.Generic.IReadOnlyList<ICombatant> combatants)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant != null && combatant.IsStunned)
                    StaggerSystem.ClearStunAtTurnEnd(combatant);
            }
        }

        private void EnterExit(CombatEndReason reason)
        {
            if (_exited)
                return;

            _exited = true;
            EndReason = reason;
            _session?.CurrentTurn?.CompleteForExit();
            SetPhase(Phase.ExitCombat);
        }

        private void SetPhase(Phase next)
        {
            if (Phase == next)
                return;

            Phase previous = Phase;
            Phase = next;
            if (OnPhaseChanged == null)
                return;

            Delegate[] handlers = OnPhaseChanged.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<Phase, Phase>)handlers[i]).Invoke(previous, next);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
