using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Core;

namespace Game.Combat.Core
{
    public sealed class CombatEntryPoint : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatFlowOrchestrator flowOrchestrator;

        [Header("Skill Book Sources (MVP)")]
        [SerializeField] private SkillDefinitionSO[] skillDefinitions;

        [Header("Default Inspiration")]
        [SerializeField] private int inspirationMax = 10;
        [SerializeField] private int inspirationStart = 3;

        [Header("Field Outcome")]
        [SerializeField] private bool deactivateDefeatedEnemies = true;
        [SerializeField] private bool destroyDefeatedEnemies = false;

        public event Action<CombatSession> OnCombatStarted;
        public event Action<CombatResult> OnCombatEnded;

        public CombatSession ActiveSession { get; private set; }
        public CombatStateMachine ActiveStateMachine { get; private set; }

        private SkillBook _book;
        private bool _endedRaised;
        private bool _startingCombat;
        private bool _missingGameStateMachineWarned;
        private bool _duplicateStartWarned;

        private void Awake()
        {
            if (flowOrchestrator == null)
                flowOrchestrator = FindFirstObjectByType<CombatFlowOrchestrator>();

            _book = new SkillBook();
            if (skillDefinitions == null)
                return;

            for (int i = 0; i < skillDefinitions.Length; i++)
            {
                SkillDefinitionSO skillDefinition = skillDefinitions[i];
                if (skillDefinition != null)
                    _book.Register(new SoSkill(skillDefinition));
            }
        }

        private void Update()
        {
            if (ActiveStateMachine == null)
                return;

            ActiveStateMachine.Tick();

            if (!_endedRaised && ActiveStateMachine.Phase == Phase.ExitCombat)
                FinishCombat(ActiveStateMachine.EndReason);

#if UNITY_EDITOR
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                Debug.Log("<color=cyan>[Debug]</color> 강제 전투 승리!");
                ForceFinishCombat(CombatEndReason.Victory);
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
            {
                Debug.Log("<color=red>[Debug]</color> 강제 전투 패배!");
                ForceFinishCombat(CombatEndReason.Defeat);
            }
#endif
        }

        public void ConfirmPlanningFromUI()
        {
            Debug.Log($"[CombatEntryPoint] ConfirmPlanningFromUI | stateMachineNull={ActiveStateMachine == null}");
            ActiveStateMachine?.ConfirmPlanning();
        }

        public bool SubmitCurrentTurn()
        {
            if (ActiveSession == null)
            {
                Debug.LogWarning("[CombatEntryPoint] SubmitCurrentTurn failed. ActiveSession is null.", this);
                return false;
            }

            if (ActiveStateMachine == null)
            {
                Debug.LogWarning("[CombatEntryPoint] SubmitCurrentTurn failed. ActiveStateMachine is null.", this);
                return false;
            }

            if (ActiveStateMachine.Phase != Phase.Planning)
            {
                Debug.LogWarning($"[CombatEntryPoint] SubmitCurrentTurn ignored. Current phase is {ActiveStateMachine.Phase}.", this);
                return false;
            }

            if (ActiveSession.CurrentTurn == null)
            {
                Debug.LogWarning("[CombatEntryPoint] SubmitCurrentTurn failed. CurrentTurn is null.", this);
                return false;
            }

            CombatTurnResolver.ResolveTurn(ActiveSession);
            ActiveStateMachine.ConfirmPlanning();

            Debug.Log(
                $"[CombatEntryPoint] Turn submitted. Turn={ActiveSession.TurnIndex}, " +
                $"Events={ActiveSession.CurrentTurn.Events.Count}, " +
                $"Playbook={ActiveSession.CurrentTurn.Playbook.Count}",
                this
            );

            return true;
        }

        public bool StartCombatFromField(
            List<GameObject> allyFieldObjects,
            List<GameObject> enemyFieldObjects,
            StartReason reason,
            Side initiativeSide,
            OpeningEffectSO openingEffectOrNull)
        {
            CombatStartRequest request = new CombatStartRequest(
                reason,
                initiativeSide,
                inspirationMax,
                inspirationStart,
                openingEffectOrNull
            );

            AddFirstFieldObject(request.AllyFieldObjects, allyFieldObjects);
            AddFirstFieldObject(request.EnemyFieldObjects, enemyFieldObjects);

            return StartCombat(request);
        }

        public bool StartCombat(CombatStartRequest request)
        {
            if (!CanStartCombatFromField())
            {
                WarnDuplicateStartBlocked();
                return false;
            }

            if (request == null)
            {
                Debug.LogWarning("[CombatEntryPoint] StartCombat called with a null request.", this);
                return false;
            }

            if (request.AllyFieldObjects == null || request.AllyFieldObjects.Count == 0)
                Debug.LogWarning("[CombatEntryPoint] StartCombat called with no ally field objects.", this);

            if (request.EnemyFieldObjects == null || request.EnemyFieldObjects.Count == 0)
                Debug.LogWarning("[CombatEntryPoint] StartCombat called with no enemy field objects.", this);

            _endedRaised = false;
            _startingCombat = true;

            int resolvedInspirationMax = request.InspirationMax > 0 ? request.InspirationMax : inspirationMax;
            int resolvedInspirationStart = request.InspirationStart >= 0 ? request.InspirationStart : inspirationStart;

            CombatStartRequest resolvedRequest = new CombatStartRequest(
                request.Reason,
                request.InitiativeSide,
                resolvedInspirationMax,
                resolvedInspirationStart,
                request.OpeningEffectOrNull
            );

            AddFirstFieldObject(resolvedRequest.AllyFieldObjects, request.AllyFieldObjects);
            AddFirstFieldObject(resolvedRequest.EnemyFieldObjects, request.EnemyFieldObjects);

            FieldCombatantFactory factory = new FieldCombatantFactory(_book);
            (ActiveSession, ActiveStateMachine) = CombatBootstrapper.StartCombat(resolvedRequest, _book, factory);

            if (ActiveSession == null)
            {
                Debug.LogError("[CombatEntryPoint] CombatBootstrapper returned a null session.", this);
                ActiveStateMachine = null;
                _startingCombat = false;
                return false;
            }

            if (ActiveStateMachine == null)
            {
                Debug.LogError("[CombatEntryPoint] CombatBootstrapper returned a null state machine.", this);
                ActiveSession = null;
                _startingCombat = false;
                return false;
            }

            if (ActiveSession.Allies.Count == 0)
                Debug.LogError("[CombatEntryPoint] Combat start produced no allies. Check player HP component and ally field object binding.", this);

            if (ActiveSession.Enemies.Count == 0)
                Debug.LogError("[CombatEntryPoint] Combat start produced no enemies. Check enemy HP component, active state, and encounter group binding.", this);

            if (flowOrchestrator != null)
                flowOrchestrator.BindSession(ActiveSession);

            if (ActiveStateMachine.Phase == Phase.EnterCombat)
            {
                ActiveStateMachine.Tick();
                Debug.Log($"[CombatEntryPoint] Forced first tick. Phase={ActiveStateMachine.Phase}, Turn={ActiveSession.TurnIndex}", this);
            }

            if (director != null)
                ActiveStateMachine.OnRequireResolutionPlay += director.PlayResolution;

            SetCombatPlanningState();

            Debug.Log(
                $"[CombatEntryPoint] Combat started. Reason={resolvedRequest.Reason}, Initiative={resolvedRequest.InitiativeSide}, " +
                $"Allies={ActiveSession.Allies.Count}, Enemies={ActiveSession.Enemies.Count}",
                this
            );

            OnCombatStarted?.Invoke(ActiveSession);

            _startingCombat = false;
            _duplicateStartWarned = false;
            return true;
        }

        private static void AddFirstFieldObject(List<GameObject> destination, List<GameObject> source)
        {
            if (destination == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject fieldObject = source[i];
                if (fieldObject == null)
                    continue;

                destination.Add(fieldObject);
                return;
            }
        }

        private bool CanStartCombatFromField()
        {
            if (_startingCombat || ActiveSession != null || ActiveStateMachine != null)
                return false;

            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private void SetCombatPlanningState()
        {
            if (GameStateMachine.Instance == null)
            {
                if (!_missingGameStateMachineWarned)
                {
                    _missingGameStateMachineWarned = true;
                    Debug.LogWarning("[CombatEntryPoint] GameStateMachine is missing. Combat started, but exploration input/UI state cannot be locked through GameState.", this);
                }

                return;
            }

            if (!GameStateMachine.Instance.Is(GameState.CombatPlanning))
                GameStateMachine.Instance.SetState(GameState.CombatPlanning);
        }

        private void WarnDuplicateStartBlocked()
        {
            if (_duplicateStartWarned)
                return;

            _duplicateStartWarned = true;
            Debug.LogWarning(
                $"[CombatEntryPoint] Duplicate or invalid combat start blocked. " +
                $"starting={_startingCombat}, activeSession={ActiveSession != null}, " +
                $"activeStateMachine={ActiveStateMachine != null}, " +
                $"gameState={(GameStateMachine.Instance != null ? GameStateMachine.Instance.Current.ToString() : "<missing>")}",
                this
            );
        }

        private void FinishCombat(CombatEndReason reason)
        {
            if (_endedRaised)
                return;

            _endedRaised = true;

            CombatSession endingSession = ActiveSession;
            CombatStateMachine endingStateMachine = ActiveStateMachine;

            if (director != null && endingStateMachine != null)
                endingStateMachine.OnRequireResolutionPlay -= director.PlayResolution;

            ApplyCombatOutcomeToField(endingSession);

            if (reason == CombatEndReason.None)
                reason = CombatEndEvaluator.Evaluate(endingSession);

            CombatResult result = CombatResultBuilder.Build(endingSession, reason);
            OnCombatEnded?.Invoke(result);

            ActiveSession = null;
            ActiveStateMachine = null;
            _startingCombat = false;
        }

#if UNITY_EDITOR
        private void ForceFinishCombat(CombatEndReason reason)
        {
            if (ActiveStateMachine == null)
                return;

            ActiveStateMachine.ForceExit(reason);
            FinishCombat(reason);
        }
#endif

        private void ApplyCombatOutcomeToField(CombatSession session)
        {
            if (session == null)
                return;

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                ICombatant combatant = session.Enemies[i];
                if (combatant == null || combatant.HP > 0)
                    continue;

                FieldCombatantAdapter adapter = combatant as FieldCombatantAdapter;
                if (adapter == null)
                    continue;

                GameObject fieldObject = adapter.FieldObject;
                if (fieldObject == null)
                    continue;

                if (destroyDefeatedEnemies)
                    Destroy(fieldObject);
                else if (deactivateDefeatedEnemies)
                    fieldObject.SetActive(false);
            }
        }
    }
}
