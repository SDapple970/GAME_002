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
        private bool _missingGameFlowControllerWarned;
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

            AddFieldObjects(request.AllyFieldObjects, allyFieldObjects);
            AddFieldObjects(request.EnemyFieldObjects, enemyFieldObjects);

            return StartCombat(request);
        }

        public bool StartCombat(CombatStartRequest request)
        {
            int requestedAllyCount = request != null && request.AllyFieldObjects != null ? request.AllyFieldObjects.Count : 0;
            int requestedEnemyCount = request != null && request.EnemyFieldObjects != null ? request.EnemyFieldObjects.Count : 0;

            if (_startingCombat || ActiveSession != null || ActiveStateMachine != null)
            {
                WarnDuplicateStartBlocked(request, requestedAllyCount, requestedEnemyCount);
                return false;
            }

            if (request == null)
            {
                LogStartRejected("request is null", null, 0, 0, 0, 0);
                return false;
            }

            GameStateMachine stateMachine = GameStateMachine.Instance;
            GameFlowController gameFlow = GameFlowController.Instance;
            if (stateMachine == null)
            {
                LogStartRejected("required GameStateMachine is missing", request, requestedAllyCount, requestedEnemyCount, 0, 0);
                return false;
            }

            if (gameFlow == null)
            {
                LogStartRejected("required GameFlowController is missing", request, requestedAllyCount, requestedEnemyCount, 0, 0);
                return false;
            }

            if (!stateMachine.Is(GameState.Exploration))
            {
                LogStartRejected($"global GameState is {stateMachine.Current}, not Exploration", request, requestedAllyCount, requestedEnemyCount, 0, 0);
                return false;
            }

            if (!TryNormalizeRequest(request, out NormalizedCombatStart normalized, out string normalizationError, out GameObject normalizationOffender))
            {
                LogStartRejected(
                    normalizationError,
                    request,
                    requestedAllyCount,
                    requestedEnemyCount,
                    normalized != null ? normalized.Allies.Length : 0,
                    normalized != null ? normalized.Enemies.Length : 0,
                    normalizationOffender);
                return false;
            }

            if (!TryValidateRoster(normalized, out string validationError, out GameObject validationOffender))
            {
                LogStartRejected(
                    validationError,
                    request,
                    requestedAllyCount,
                    requestedEnemyCount,
                    normalized.Allies.Length,
                    normalized.Enemies.Length,
                    validationOffender);
                return false;
            }

            _startingCombat = true;
            _endedRaised = false;
            CombatSession createdSession = null;
            CombatStateMachine createdStateMachine = null;
            bool orchestratorBound = false;
            bool phaseSubscribed = false;
            bool directorSubscribed = false;

            try
            {
                CombatStartRequest startupRequest = normalized.CreateRequest();
                FieldCombatantFactory factory = new FieldCombatantFactory(_book);
                (createdSession, createdStateMachine) = CombatBootstrapper.StartCombat(startupRequest, _book, factory);

                if (createdSession == null)
                    throw new InvalidOperationException("CombatBootstrapper returned a null CombatSession.");

                if (createdStateMachine == null)
                    throw new InvalidOperationException("CombatBootstrapper returned a null CombatStateMachine.");

                VerifyConstructedRoster(createdSession, normalized);

                ActiveSession = createdSession;
                ActiveStateMachine = createdStateMachine;

                if (flowOrchestrator != null)
                {
                    flowOrchestrator.BindSession(createdSession);
                    orchestratorBound = true;
                }

                createdStateMachine.OnPhaseChanged += HandleCombatPhaseChanged;
                phaseSubscribed = true;

                if (director != null)
                {
                    createdStateMachine.OnRequireResolutionPlay += director.PlayResolution;
                    directorSubscribed = true;
                }

                if (!TrySynchronizeGlobalCombatState(createdStateMachine.Phase))
                    throw new InvalidOperationException($"Global combat state synchronization failed for phase {createdStateMachine.Phase}.");

                Debug.Log(
                    $"[CombatEntryPoint] Combat started. Reason={normalized.Reason}, Initiative={normalized.InitiativeSide}, " +
                    $"Allies={createdSession.Allies.Count}, Enemies={createdSession.Enemies.Count}",
                    this);

                RaiseCombatStarted(createdSession);
                _duplicateStartWarned = false;
                return true;
            }
            catch (Exception exception)
            {
                RollbackStartup(createdStateMachine, orchestratorBound, phaseSubscribed, directorSubscribed);
                Debug.LogError(
                    $"[CombatEntryPoint] Combat startup rolled back. Reason={request.Reason}, " +
                    $"requestedAllies={requestedAllyCount}, requestedEnemies={requestedEnemyCount}, " +
                    $"validAllies={normalized.Allies.Length}, validEnemies={normalized.Enemies.Length}, " +
                    $"failure={exception.GetType().Name}: {exception.Message}",
                    this);
                return false;
            }
            finally
            {
                _startingCombat = false;
            }
        }

        private static void AddFieldObjects(List<GameObject> destination, List<GameObject> source)
        {
            if (destination == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject fieldObject = source[i];
                if (fieldObject == null || destination.Contains(fieldObject))
                    continue;

                destination.Add(fieldObject);
            }
        }

        private bool TryNormalizeRequest(
            CombatStartRequest request,
            out NormalizedCombatStart normalized,
            out string error,
            out GameObject offendingObject)
        {
            normalized = null;
            error = null;
            offendingObject = null;

            List<GameObject> uniqueAllies = CollectUniqueNonNull(request.AllyFieldObjects);
            List<GameObject> uniqueEnemies = CollectUniqueNonNull(request.EnemyFieldObjects);
            HashSet<GameObject> allySet = new HashSet<GameObject>(uniqueAllies);

            GameObject[] activeAllies = FilterActive(uniqueAllies);
            GameObject[] activeEnemies = FilterActive(uniqueEnemies);

            int resolvedInspirationMax = request.InspirationMax > 0
                ? request.InspirationMax
                : Mathf.Max(1, inspirationMax);
            int resolvedDefaultStart = Mathf.Clamp(inspirationStart, 0, resolvedInspirationMax);
            int resolvedInspirationStart = request.InspirationStart >= 0
                ? Mathf.Clamp(request.InspirationStart, 0, resolvedInspirationMax)
                : resolvedDefaultStart;

            normalized = new NormalizedCombatStart(
                request.Reason,
                request.InitiativeSide,
                resolvedInspirationMax,
                resolvedInspirationStart,
                request.OpeningEffectOrNull,
                activeAllies,
                activeEnemies);

            for (int i = 0; i < uniqueEnemies.Count; i++)
            {
                GameObject enemy = uniqueEnemies[i];
                if (!allySet.Contains(enemy))
                    continue;

                offendingObject = enemy;
                error = "the same field object was assigned to both ally and enemy sides";
                return false;
            }

            if (activeAllies.Length == 0)
            {
                error = uniqueAllies.Count == 0
                    ? "request contains no non-null allies"
                    : "every ally was removed because inactive field objects are not eligible";
                return false;
            }

            if (activeEnemies.Length == 0)
            {
                error = uniqueEnemies.Count == 0
                    ? "request contains no non-null enemies"
                    : "every enemy was removed because inactive field objects are not eligible";
                return false;
            }

            return true;
        }

        private static List<GameObject> CollectUniqueNonNull(List<GameObject> source)
        {
            int capacity = source != null ? source.Count : 0;
            List<GameObject> result = new List<GameObject>(capacity);
            HashSet<GameObject> seen = new HashSet<GameObject>();

            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && seen.Add(candidate))
                    result.Add(candidate);
            }

            return result;
        }

        private static GameObject[] FilterActive(List<GameObject> source)
        {
            List<GameObject> active = new List<GameObject>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy)
                    active.Add(candidate);
            }

            return active.ToArray();
        }

        private static bool TryValidateRoster(
            NormalizedCombatStart normalized,
            out string error,
            out GameObject offendingObject)
        {
            error = null;
            offendingObject = null;
            ValidateSide(normalized.Allies, "ally", ref error, ref offendingObject);
            ValidateSide(normalized.Enemies, "enemy", ref error, ref offendingObject);
            return error == null;
        }

        private static void ValidateSide(
            GameObject[] fieldObjects,
            string sideName,
            ref string firstError,
            ref GameObject firstOffender)
        {
            for (int i = 0; i < fieldObjects.Length; i++)
            {
                GameObject fieldObject = fieldObjects[i];
                if (TryValidateFieldCombatant(fieldObject, out string validationError) || firstError != null)
                    continue;

                firstError = $"{sideName} combatant {validationError}";
                firstOffender = fieldObject;
            }
        }

        private static bool TryValidateFieldCombatant(GameObject fieldObject, out string error)
        {
            error = null;
            try
            {
                HpAccessor accessor = HpAccessor.TryCreate(fieldObject);
                if (accessor == null || !accessor.IsValid || accessor.SourceComponent == null)
                {
                    error = "cannot be adapted through the HP accessor contract";
                    return false;
                }

                int hp = accessor.GetHp();
                int maxHp = accessor.GetMaxHpOrCurrent();
                if (maxHp <= 0 || hp < 0 || hp > maxHp)
                {
                    error = $"has an invalid HP source (HP={hp}, MaxHP={maxHp})";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"has an unreadable HP source ({exception.GetType().Name}: {exception.Message})";
                return false;
            }
        }

        private static void VerifyConstructedRoster(CombatSession session, NormalizedCombatStart normalized)
        {
            if (session.Allies.Count != normalized.Allies.Length || session.Enemies.Count != normalized.Enemies.Length)
            {
                throw new InvalidOperationException(
                    $"Constructed roster count mismatch. Expected {normalized.Allies.Length}v{normalized.Enemies.Length}, " +
                    $"received {session.Allies.Count}v{session.Enemies.Count}.");
            }

            HashSet<int> combatantIds = new HashSet<int>();
            VerifyConstructedSide(session.Allies, normalized.Allies, Side.Allies, combatantIds);
            VerifyConstructedSide(session.Enemies, normalized.Enemies, Side.Enemies, combatantIds);
        }

        private static void VerifyConstructedSide(
            List<ICombatant> combatants,
            GameObject[] expectedFieldObjects,
            Side expectedSide,
            HashSet<int> combatantIds)
        {
            for (int i = 0; i < expectedFieldObjects.Length; i++)
            {
                ICombatant combatant = combatants[i];
                FieldCombatantAdapter adapter = combatant as FieldCombatantAdapter;
                if (adapter == null || adapter.FieldObject != expectedFieldObjects[i] || combatant.Side != expectedSide)
                    throw new InvalidOperationException($"Constructed {expectedSide} roster differs from the normalized request at index {i}.");

                if (!combatantIds.Add(combatant.Id.Value))
                    throw new InvalidOperationException($"Duplicate CombatantId {combatant.Id.Value} was produced.");
            }
        }

        private void RollbackStartup(
            CombatStateMachine stateMachine,
            bool orchestratorBound,
            bool phaseSubscribed,
            bool directorSubscribed)
        {
            if (stateMachine != null && phaseSubscribed)
                stateMachine.OnPhaseChanged -= HandleCombatPhaseChanged;

            if (stateMachine != null && director != null && directorSubscribed)
                stateMachine.OnRequireResolutionPlay -= director.PlayResolution;

            if (orchestratorBound && flowOrchestrator != null)
                flowOrchestrator.BindSession(null);

            ActiveSession = null;
            ActiveStateMachine = null;
            _endedRaised = false;

            GameStateMachine state = GameStateMachine.Instance;
            if (state != null && state.IsCombatState() && GameFlowController.Instance != null)
            {
                try
                {
                    GameFlowController.Instance.EnterExploration();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CombatEntryPoint] Failed to restore Exploration during startup rollback: {exception.Message}", this);
                }
            }
        }

        private void RaiseCombatStarted(CombatSession session)
        {
            Action<CombatSession> handlers = OnCombatStarted;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatSession>)invocationList[i]).Invoke(session);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CombatEntryPoint] OnCombatStarted subscriber failed: {exception}", this);
                }
            }
        }

        private void LogStartRejected(
            string rejection,
            CombatStartRequest request,
            int requestedAllies,
            int requestedEnemies,
            int validAllies,
            int validEnemies,
            GameObject offendingObject = null)
        {
            Debug.LogWarning(
                $"[CombatEntryPoint] Combat start rejected. rejection={rejection}; " +
                $"reason={(request != null ? request.Reason.ToString() : "<null request>")}; " +
                $"requestedAllies={requestedAllies}; requestedEnemies={requestedEnemies}; " +
                $"validAllies={validAllies}; validEnemies={validEnemies}; " +
                $"offender={(offendingObject != null ? offendingObject.name : "<none>")}.",
                this);
        }

        private sealed class NormalizedCombatStart
        {
            public readonly StartReason Reason;
            public readonly Side InitiativeSide;
            public readonly int InspirationMax;
            public readonly int InspirationStart;
            public readonly OpeningEffectSO OpeningEffectOrNull;
            public readonly GameObject[] Allies;
            public readonly GameObject[] Enemies;

            public NormalizedCombatStart(
                StartReason reason,
                Side initiativeSide,
                int inspirationMax,
                int inspirationStart,
                OpeningEffectSO openingEffectOrNull,
                GameObject[] allies,
                GameObject[] enemies)
            {
                Reason = reason;
                InitiativeSide = initiativeSide;
                InspirationMax = inspirationMax;
                InspirationStart = inspirationStart;
                OpeningEffectOrNull = openingEffectOrNull;
                Allies = allies;
                Enemies = enemies;
            }

            public CombatStartRequest CreateRequest()
            {
                CombatStartRequest request = new CombatStartRequest(
                    Reason,
                    InitiativeSide,
                    InspirationMax,
                    InspirationStart,
                    OpeningEffectOrNull);
                request.AllyFieldObjects.AddRange(Allies);
                request.EnemyFieldObjects.AddRange(Enemies);
                return request;
            }
        }

        private void HandleCombatPhaseChanged(Phase previous, Phase next)
        {
            SynchronizeGlobalCombatState(next);
        }

        private void SynchronizeGlobalCombatState(Phase phase)
        {
            TrySynchronizeGlobalCombatState(phase);
        }

        private bool TrySynchronizeGlobalCombatState(Phase phase)
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            if (stateMachine == null)
            {
                if (!_missingGameStateMachineWarned)
                {
                    _missingGameStateMachineWarned = true;
                    Debug.LogWarning("[CombatEntryPoint] GameStateMachine is missing. Combat started, but exploration input/UI state cannot be locked through GameState.", this);
                }

                return false;
            }

            GameFlowController flow = GameFlowController.Instance;
            if (flow == null)
            {
                if (!_missingGameFlowControllerWarned)
                {
                    _missingGameFlowControllerWarned = true;
                    Debug.LogWarning("[CombatEntryPoint] GameFlowController is missing. Production global combat state cannot be synchronized.", this);
                }

                return false;
            }

            if (phase == Phase.Planning)
            {
                flow.EnterCombatPlanning();
                return stateMachine.Is(GameState.CombatPlanning);
            }
            else if (phase == Phase.Resolution || phase == Phase.EndTurn)
            {
                flow.EnterCombatResolving();
                return stateMachine.Is(GameState.CombatResolving);
            }

            return true;
        }

        private void WarnDuplicateStartBlocked(CombatStartRequest request, int requestedAllies, int requestedEnemies)
        {
            if (_duplicateStartWarned)
                return;

            _duplicateStartWarned = true;
            Debug.LogWarning(
                $"[CombatEntryPoint] Duplicate or invalid combat start blocked. " +
                $"starting={_startingCombat}, activeSession={ActiveSession != null}, " +
                $"activeStateMachine={ActiveStateMachine != null}, " +
                $"gameState={(GameStateMachine.Instance != null ? GameStateMachine.Instance.Current.ToString() : "<missing>")}, " +
                $"reason={(request != null ? request.Reason.ToString() : "<null request>")}, " +
                $"requestedAllies={requestedAllies}, requestedEnemies={requestedEnemies}",
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

            if (endingStateMachine != null)
                endingStateMachine.OnPhaseChanged -= HandleCombatPhaseChanged;

            if (director != null && endingStateMachine != null)
                endingStateMachine.OnRequireResolutionPlay -= director.PlayResolution;

            if (flowOrchestrator != null)
                flowOrchestrator.BindSession(null);

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
