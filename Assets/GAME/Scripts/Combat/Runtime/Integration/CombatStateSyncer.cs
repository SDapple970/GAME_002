// Scripts/Integration/CombatStateSyncer.cs
using UnityEngine;
using Game.Core;
using Game.Combat.Core; // CombatEntryPoint가 있는 네임스페이스
using Game.Combat.Model;

namespace Game.Integration
{
    /// <summary>
    /// 전투 시스템이 시작될 때 GameState를 Combat으로 동기화한다.
    /// 전투 종료 후 Exploration 복귀는 RewardUIPanel 등 후속 UI 흐름이 담당한다.
    /// </summary>
    public sealed class CombatStateSyncer : MonoBehaviour
    {
        [Tooltip("상태를 감지할 전투 메인 매니저")]
        [SerializeField] private CombatEntryPoint entryPoint;

        private bool _subscribedToEntryPoint;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToEntryPoint();
        }

        private void OnDisable()
        {
            UnsubscribeFromEntryPoint();
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        private void SubscribeToEntryPoint()
        {
            if (_subscribedToEntryPoint)
                return;

            if (entryPoint == null)
            {
                Debug.LogWarning("[CombatStateSyncer] CombatEntryPoint is not assigned.", this);
                return;
            }

            entryPoint.OnCombatStarted -= HandleCombatStarted;
            entryPoint.OnCombatEnded -= HandleCombatEnded;
            entryPoint.OnCombatStarted += HandleCombatStarted;
            entryPoint.OnCombatEnded += HandleCombatEnded;
            _subscribedToEntryPoint = true;
        }

        private void UnsubscribeFromEntryPoint()
        {
            if (!_subscribedToEntryPoint || entryPoint == null)
            {
                _subscribedToEntryPoint = false;
                return;
            }

            entryPoint.OnCombatStarted -= HandleCombatStarted;
            entryPoint.OnCombatEnded -= HandleCombatEnded;
            _subscribedToEntryPoint = false;
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

        private void HandleCombatEnded(CombatResult result)
        {
            // Intentionally do not restore Exploration here.
            // RewardUIPanel or another post-combat UI may need to hold UIOnly first.
        }
    }
}
