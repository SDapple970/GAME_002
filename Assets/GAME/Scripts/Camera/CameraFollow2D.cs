// 위치: GAME/Scripts/Camera/CameraFollow2D.cs
using UnityEngine;

namespace Game.CameraSys
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, -10f);
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Zoom Settings")]
        [Tooltip("필드 탐험 시 기본 카메라 줌 크기")]
        [SerializeField] private float defaultOrthoSize = 5f;
        [Tooltip("줌 인/아웃 전환에 걸리는 시간")]
        [SerializeField] private float zoomSmoothTime = 0.2f;

        private Vector3 _vel;
        private float _zoomVel;
        private Camera _cam;
        private float _targetOrthoSize;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _targetOrthoSize = defaultOrthoSize;
            if (_cam != null) _cam.orthographicSize = defaultOrthoSize;
        }

        private void LateUpdate()
        {
            // 1. 부드러운 위치 이동 (SmoothDamp)
            if (target != null)
            {
                var desired = target.position + offset;
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            }

            // 2. 부드러운 줌 인/줌 아웃 (SmoothDamp)
            if (_cam != null)
            {
                _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _targetOrthoSize, ref _zoomVel, zoomSmoothTime);
            }
        }

        // 🌟 외부(전투 매니저 등)에서 타겟과 줌을 제어하기 위한 퍼블릭 함수들
        public void SetTarget(Transform newTarget) => target = newTarget;
        public Transform GetTarget() => target;

        public void SetZoom(float size) => _targetOrthoSize = size;
        public void ResetZoom() => _targetOrthoSize = defaultOrthoSize;
    }
}