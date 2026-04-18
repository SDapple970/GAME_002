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
            ActiveStateMachine?.ConfirmPlanning();
        }

        private void Awake()
        {
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