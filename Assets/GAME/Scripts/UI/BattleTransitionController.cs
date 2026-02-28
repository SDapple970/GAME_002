// Scripts/Battle/BattleTransitionController.cs
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.UI;
using Game.Combat.Core; // CombatEntryPoint를 사용하기 위해 필요

namespace Game.Battle
{
    public sealed class BattleTransitionController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ScreenFader fader;
        [Tooltip("전투를 실제로 시작할 매니저 스크립트")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;

        [Header("UI Canvas References")]
        [Tooltip("평상시 탐험 UI (없으면 비워둬도 됨)")]
        [SerializeField] private GameObject overworldCanvas;
        [Tooltip("전투용 UI (Canvas_Combat)")]
        [SerializeField] private GameObject combatCanvas;

        private void OnEnable()
        {
            BattleTrigger2D.OnBattleRequested += HandleBattleRequested;
        }

        private void OnDisable()
        {
            BattleTrigger2D.OnBattleRequested -= HandleBattleRequested;
        }

        private void HandleBattleRequested(BattleTransitionRequest req)
        {
            if (GameStateMachine.Instance == null) return;

            // 현재 탐험 상태가 아니면 중복 실행 방지
            if (!GameStateMachine.Instance.Is(GameState.Exploration)) return;

            StartCoroutine(Co_TransitionToSeamlessCombat(req));
        }

        private IEnumerator Co_TransitionToSeamlessCombat(BattleTransitionRequest req)
        {
            // 1. 상태를 '전투 전환 중'으로 변경 (이때부터 플레이어 조작 차단됨!)
            GameStateMachine.Instance.SetState(GameState.CombatTransition);

            // 2. 화면 페이드 아웃 (검은 화면)
            if (fader != null) yield return fader.FadeOut(this);

            // 3. UI 교체 (탐험 UI 끄고, 전투 UI 켜기)
            if (overworldCanvas != null) overworldCanvas.SetActive(false);
            if (combatCanvas != null) combatCanvas.SetActive(true);

            // 4. 상태를 '전투'로 확정 (여기서 플레이어가 확실히 멈춥니다!)
            GameStateMachine.Instance.SetState(GameState.Combat);

            // 5. 화면 페이드 인 (화면 밝아짐)
            if (fader != null) yield return fader.FadeIn(this);
        }
    }
}