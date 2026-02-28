using UnityEngine;
using Game.Core; // GameStateMachine 및 GameState 사용
using Game.Battle; // BattleTrigger2D 및 관련 구조체 사용

namespace Game.Battle
{
    /// <summary>
    /// 씬 전환 없이 그 자리에서 즉시 전투를 시작하는 심리스 매니저
    /// </summary>
    public sealed class SeamlessBattleManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("전투 시작 시 활성화될 UI 루트 오브젝트 (예: 스킬창, 로그창)")]
        [SerializeField] private GameObject combatUIRoot;

        private void OnEnable()
        {
            // 1. 전투 요청 이벤트 구독 시작
            BattleTrigger2D.OnBattleRequested += HandleSeamlessBattle;
        }

        private void OnDisable()
        {
            // 2. 이벤트 구독 해제 (메모리 누수 방지)
            BattleTrigger2D.OnBattleRequested -= HandleSeamlessBattle;
        }

        private void HandleSeamlessBattle(BattleTransitionRequest req)
        {
            // 3. 게임 상태를 전투(Combat)로 변경
            // 상태가 바뀌면 PlayerController2D에서 조작 입력을 자동으로 차단함
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.SetState(GameState.Combat);
            }

            // 4. 심리스 연출: 암전 없이 전투용 UI만 즉시 활성화
            if (combatUIRoot != null)
            {
                combatUIRoot.SetActive(true);
            }

            // 5. 디버그 로그로 발생 지점 확인
            Debug.Log($"[Seamless] {req.EncounterWorldPos} 지점에서 전투 발생!");

            // TODO: 이후 여기에 적 오브젝트 생성이나 전투 시스템 초기화 로직을 추가
        }
    }
}