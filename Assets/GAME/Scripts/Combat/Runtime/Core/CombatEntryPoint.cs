using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Combat.Effects; // ✅ Director 사용을 위해 추가
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CombatEntryPoint : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private CombatDirector director; // ✅ 시각화 연출 디렉터 연결

        [Header("Skill Book Sources (MVP)")]
        [SerializeField] private SkillDefinitionSO[] skillDefinitions;

        [Header("Default Inspiration")]
        [SerializeField] private int inspirationMax = 10;
        [SerializeField] private int inspirationStart = 3;

        [SerializeField] private bool deactivateDefeatedEnemies = true;
        [SerializeField] private bool destroyDefeatedEnemies = false; // true면 Destroy, false면 SetActive(false)

        public event Action<CombatSession> OnCombatStarted;
        public event Action<CombatResult> OnCombatEnded;


        public CombatSession ActiveSession { get; private set; }
        public CombatStateMachine ActiveStateMachine { get; private set; }

        private SkillBook _book;
        private bool _endedRaised;

        // ✅ UI에서 이 함수만 호출하면 됨
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

                // ✅ 전투 종료 시 Director 이벤트 해제
                if (director != null)
                {
                    ActiveStateMachine.OnRequireResolutionPlay -= director.PlayResolution;
                }

                ApplyCombatOutcomeToField(ActiveSession);

                var result = new CombatResult
                {
                    IsWin = true,      // 임시로 무조건 승리했다고 가정
                    TotalExp = 150,    // 임시 경험치
                    TotalGold = 50     // 임시 골드
                };

                OnCombatEnded?.Invoke(result);

                ActiveSession = null;
                ActiveStateMachine = null;
            }
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

            // ✅ Director에게 StateMachine의 연출 지시 이벤트를 연결
            if (director != null)
            {
                ActiveStateMachine.OnRequireResolutionPlay += director.PlayResolution;
            }

            Debug.Log($"[EntryPoint] Combat started. Reason={reason}, Initiative={initiativeSide}, Allies={ActiveSession.Allies.Count}, Enemies={ActiveSession.Enemies.Count}");

            OnCombatStarted?.Invoke(ActiveSession);
        }

        private void ApplyCombatOutcomeToField(CombatSession session)
        {
            if (session == null) return;

            // 적 전투원 중 HP 0인 애들의 필드 오브젝트를 비활성/삭제
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                var c = session.Enemies[i];
                if (c == null) continue;
                if (c.HP > 0) continue;

                // FieldCombatantAdapter일 때만 필드 오브젝트를 알 수 있음
                if (c is Game.Combat.Adapters.FieldCombatantAdapter fa)
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