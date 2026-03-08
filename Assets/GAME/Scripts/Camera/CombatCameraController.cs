// 위치: GAME/Scripts/Combat/Integration/CombatCameraController.cs
using UnityEngine;
using Game.Combat.Core;
using Game.CameraSys;
using Game.Combat.Adapters;
using Game.Combat.Model; // 🌟 [수정 포인트] 이 줄이 추가되었어!

namespace Game.Combat.Integration
{
    /// <summary>
    /// 전투 시작 시 카메라를 전투 대형의 중앙으로 이동시키고 줌 아웃합니다.
    /// </summary>
    public sealed class CombatCameraController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CombatEntryPoint entryPoint;
        [Tooltip("씬에 있는 Main Camera (CameraFollow2D 컴포넌트)")]
        [SerializeField] private CameraFollow2D mainCamera;

        [Header("Combat Camera Settings")]
        [Tooltip("전투 중 카메라가 비출 중심점 (빈 오브젝트 할당, 없으면 자동 생성)")]
        [SerializeField] private Transform combatCenterPoint;
        [Tooltip("전투 시 카메라 줌 아웃 크기 (기본값보다 숫자가 크면 넓게 보임)")]
        [SerializeField] private float combatZoomSize = 6.5f;

        private Transform _originalTarget;

        private void Awake()
        {
            if (combatCenterPoint == null)
            {
                combatCenterPoint = new GameObject("CombatCenterPoint").transform;
            }
        }

        private void OnEnable()
        {
            if (entryPoint != null)
            {
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
            if (mainCamera == null) return;

            // 1. 기존 필드 타겟(플레이어) 기억해두기
            _originalTarget = mainCamera.GetTarget();

            // 2. 아군과 적군의 위치를 읽어와서 완벽한 중앙값(Center) 계산
            if (session.Allies.Count > 0 && session.Enemies.Count > 0)
            {
                var allyObj = ((FieldCombatantAdapter)session.Allies[0]).FieldObject;
                var enemyObj = ((FieldCombatantAdapter)session.Enemies[0]).FieldObject;

                if (allyObj != null && enemyObj != null)
                {
                    Vector3 centerPos = (allyObj.transform.position + enemyObj.transform.position) / 2f;
                    combatCenterPoint.position = centerPos;
                }
            }

            // 3. 카메라에게 새로운 타겟과 줌아웃 명령 내리기!
            mainCamera.SetTarget(combatCenterPoint);
            mainCamera.SetZoom(combatZoomSize);

            Debug.Log("[CombatCamera] 전투 카메라 뷰 전환 완료!");
        }

        private void HandleCombatEnded(CombatResult result)
        {
            if (mainCamera == null) return;

            // 전투가 끝나면 카메라를 다시 원래 플레이어에게 돌려주고 줌 복구
            if (_originalTarget != null)
            {
                mainCamera.SetTarget(_originalTarget);
            }
            mainCamera.ResetZoom();

            Debug.Log("[CombatCamera] 탐험 카메라 뷰 복구 완료!");
        }
    }
}