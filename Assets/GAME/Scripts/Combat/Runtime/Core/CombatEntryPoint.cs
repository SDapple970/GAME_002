// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatEntryPoint.cs
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Combat.Effects;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatEntryPoint : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private CombatDirector director;

        [Header("Skill Book Sources (MVP)")]
        [SerializeField] private SkillDefinitionSO[] skillDefinitions;

        [Header("Default Inspiration")]
        [SerializeField] private int inspirationMax = 10;
        [SerializeField] private int inspirationStart = 3;

        [SerializeField] private bool deactivateDefeatedEnemies = true;
        [SerializeField] private bool destroyDefeatedEnemies = false;

        [SerializeField] private CombatFlowOrchestrator flowOrchestrator;

        public event Action<CombatSession> OnCombatStarted;
        public event Action<CombatResult> OnCombatEnded;

        public CombatSession ActiveSession { get; private set; }
        public CombatStateMachine ActiveStateMachine { get; private set; }

        private SkillBook _book;
        private bool _endedRaised;

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

        private void Awake()
        {
            if (flowOrchestrator == null)
                flowOrchestrator = FindFirstObjectByType<CombatFlowOrchestrator>();

            _book = new SkillBook();
            if (skillDefinitions != null)
            {
                for (int i = 0; i < skillDefinitions.Length; i++)
                {
                    var so = skillDefinitions[i];
                    if (so == null) continue;
                    _book.Register(new SoSkill(so));
                }
            }
        }

        private void Update()
        {
            if (ActiveStateMachine == null) return;

            ActiveStateMachine.Tick();

            if (!_endedRaised && ActiveStateMachine.Phase == Phase.ExitCombat)
            {
                _endedRaised = true;

                if (director != null)
                    ActiveStateMachine.OnRequireResolutionPlay -= director.PlayResolution;

                ApplyCombatOutcomeToField(ActiveSession);

                var reason = ActiveStateMachine.EndReason;
                if (reason == CombatEndReason.None)
                    reason = CombatEndEvaluator.Evaluate(ActiveSession);

                var result = CombatResultBuilder.Build(ActiveSession, reason);

                OnCombatEnded?.Invoke(result);

                ActiveSession = null;
                ActiveStateMachine = null;
            }

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

        private void ForceFinishCombat(CombatEndReason reason)
        {
            if (ActiveStateMachine == null)
                return;

            _endedRaised = true;

            if (director != null)
                ActiveStateMachine.OnRequireResolutionPlay -= director.PlayResolution;

            ActiveStateMachine.ForceExit(reason);

            var result = CombatResultBuilder.Build(ActiveSession, reason);
            OnCombatEnded?.Invoke(result);

            ActiveSession = null;
            ActiveStateMachine = null;
        }

        public void StartCombatFromField(
            List<GameObject> allyFieldObjects,
            List<GameObject> enemyFieldObjects,
            StartReason reason,
            Side initiativeSide,
            OpeningEffectSO openingEffectOrNull)
        {
            if (allyFieldObjects == null || allyFieldObjects.Count == 0)
                Debug.LogWarning("[CombatEntryPoint] StartCombatFromField called with no ally field objects.");

            if (enemyFieldObjects == null || enemyFieldObjects.Count == 0)
                Debug.LogWarning("[CombatEntryPoint] StartCombatFromField called with no enemy field objects.");

            _endedRaised = false;

            var req = new CombatStartRequest(
                reason,
                initiativeSide,
                inspirationMax,
                inspirationStart,
                openingEffectOrNull
            );

            if (allyFieldObjects != null) req.AllyFieldObjects.AddRange(allyFieldObjects);
            if (enemyFieldObjects != null) req.EnemyFieldObjects.AddRange(enemyFieldObjects);

            var factory = new FieldCombatantFactory(_book);
            (ActiveSession, ActiveStateMachine) = CombatBootstrapper.StartCombat(req, _book, factory);

            if (ActiveSession == null)
            {
                Debug.LogError("[CombatEntryPoint] CombatBootstrapper returned a null session.");
                return;
            }

            if (ActiveSession.Allies.Count == 0)
                Debug.LogError("[CombatEntryPoint] Combat start produced no allies. Check player HP component and ally field object binding.");

            if (ActiveSession.Enemies.Count == 0)
                Debug.LogError("[CombatEntryPoint] Combat start produced no enemies. Check enemy HP component, active state, and encounter group binding.");

            if (ActiveStateMachine != null && ActiveStateMachine.Phase == Phase.EnterCombat)
            {
                ActiveStateMachine.Tick();
                Debug.Log($"[EntryPoint] Forced first tick. Phase={ActiveStateMachine.Phase}, Turn={ActiveSession.TurnIndex}");
            }

            if (director != null)
                ActiveStateMachine.OnRequireResolutionPlay += director.PlayResolution;

            Debug.Log($"[EntryPoint] Combat started. Reason={reason}, Initiative={initiativeSide}, Allies={ActiveSession.Allies.Count}, Enemies={ActiveSession.Enemies.Count}");
            OnCombatStarted?.Invoke(ActiveSession);


            if (flowOrchestrator != null)
                flowOrchestrator.BindSession(ActiveSession);
        }

        private void ApplyCombatOutcomeToField(CombatSession session)
        {
            if (session == null) return;

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                var c = session.Enemies[i];
                if (c == null) continue;
                if (c.HP > 0) continue;

                if (c is FieldCombatantAdapter fa)
                {
                    var go = fa.FieldObject;
                    if (go == null) continue;

                    if (destroyDefeatedEnemies)
                        Destroy(go);
                    else if (deactivateDefeatedEnemies)
                        go.SetActive(false);
                }
            }
        }
    }
}
