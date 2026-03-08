// 위치: GAME/Scripts/Battle/BattleTransitionController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // [수정 포인트 2] SceneManager 사용을 위한 네임스페이스 추가
using Game.Core;
using Game.UI;
using Game.Combat.Core;

namespace Game.Battle
{
    public sealed class BattleTransitionController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ScreenFader fader; // [수정 포인트 1] 이 변수 하나만 남겨두고 아래 중복 선언 삭제
        [Tooltip("전투를 실제로 시작할 매니저 스크립트")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;

        [Header("UI Canvas References")]
        [Tooltip("평상시 탐험 UI (없으면 비워둬도 됨)")]
        [SerializeField] private GameObject overworldCanvas;
        [Tooltip("전투용 UI (Canvas_Combat)")]
        [SerializeField] private GameObject combatCanvas;

        [Header("Mode")]
        [SerializeField] private bool loadBattleSceneSingle = true;

        public static BattleTransitionRequest LastEncounterRequest { get; private set; }

        private void OnEnable()
        {
            // FieldEnemy의 Action과 BattleTrigger2D의 Action 모두 동일한 델리게이트 시그니처를 가짐
            FieldEnemy.OnBattleRequested += HandleBattleRequested;
            BattleTrigger2D.OnBattleRequested += HandleBattleRequested;
        }

        private void OnDisable()
        {
            FieldEnemy.OnBattleRequested -= HandleBattleRequested;
            BattleTrigger2D.OnBattleRequested -= HandleBattleRequested;
        }

        private void HandleBattleRequested(BattleTransitionRequest req)
        {
            if (GameStateMachine.Instance == null) return;

            // 현재 탐험 상태가 아니면 중복 실행 방지
            if (!GameStateMachine.Instance.Is(GameState.Exploration)) return;

            StartCoroutine(Co_Transition(req));
        }

        private IEnumerator Co_Transition(BattleTransitionRequest req)
        {
            GameStateMachine.Instance.SetState(GameState.CombatTransition);

            if (fader != null) yield return fader.FadeOut(this);

            // 전달받은 조우 데이터 덮어쓰기
            LastEncounterRequest = req;
            Debug.Log($"[Battle Transition] 씬 전환 준비 완료. 조우 데이터: {req.Advantage}");

            if (loadBattleSceneSingle && !string.IsNullOrEmpty(req.BattleSceneName))
            {
                // SceneManager를 정상적으로 호출
                var op = SceneManager.LoadSceneAsync(req.BattleSceneName, LoadSceneMode.Single);
                while (!op.isDone) yield return null;
            }

            GameStateMachine.Instance.SetState(GameState.Combat);

            if (fader != null) yield return fader.FadeIn(this);
        }
    }
}