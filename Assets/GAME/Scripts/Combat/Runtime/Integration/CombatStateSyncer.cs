// Scripts/Integration/CombatStateSyncer.cs
using UnityEngine;
using Game.Core;
using Game.Combat.Core; // CombatEntryPoint가 있는 네임스페이스
using Game.Combat.Model;

namespace Game.Integration
{
    /// <summary>
    /// 전투 시스템이 시작/종료될 때 자동으로 게임 상태(GameStateMachine)를 
    /// Combat <-> Exploration으로 동기화해주는 연결 고리입니다.
    /// </summary>
    public sealed class CombatStateSyncer : MonoBehaviour
    {
        [Tooltip("상태를 감지할 전투 메인 매니저")]
        [SerializeField] private CombatEntryPoint entryPoint;

        private void OnEnable()
        {
            if (entryPoint != null)
            {
                // 전투가 시작/종료될 때 발생하는 이벤트를 구독합니다.
                entryPoint.OnCombatStarted += HandleCombatStarted;
                entryPoint.OnCombatEnded += HandleCombatEnded;
            }
        }

        private void OnDisable()
        {
            if (entryPoint != null)
            {
                entryPoint.OnCombatStarted -= HandleCombatStarted;
                entryPoint.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void HandleCombatStarted(CombatSession session)
        {
            // 전투가 시작되면 묻지도 따지지도 않고 상태를 Combat으로 강제 고정! (플레이어 정지)
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.SetState(GameState.Combat);
                Debug.Log("🔒 [CombatStateSyncer] 전투 시작 감지! 플레이어 조작을 차단합니다.");
            }
        }

        private void HandleCombatEnded(CombatResult result) // <- 괄호 안에 CombatResult result 추가!
        {
            // 💡 중요 포인트: 상태(GameState) 제어 충돌 방지
            // 기존에는 여기서 GameState를 Exploration(탐험)으로 돌려놨을 거야.
            // 하지만 이제 전투가 끝나면 '보상 UI(RewardUIPanel)'가 먼저 떠야 하니까 상태가 UIOnly가 돼야 해!
            // 따라서 여기서 상태를 Exploration으로 강제 변경하는 코드가 있다면 지우거나 주석 처리해 줘.

            /* 예시:
            if (GameStateMachine.Instance != null)
            {
                // GameStateMachine.Instance.SetState(GameState.Exploration); // <- 이 부분을 주석 처리!
                Debug.Log("[CombatStateSyncer] 전투 종료 이벤트 감지! (상태 전환은 보상 UI가 담당합니다)");
            }
            */
        }
    }
}