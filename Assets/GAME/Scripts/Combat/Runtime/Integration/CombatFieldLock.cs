using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Integration
{
    /// <summary>
    /// 전투 시작/종료 이벤트를 받아 필드(오버월드) 조작을 잠그고 복구한다.
    /// 기존 필드 코드를 거의 건드리지 않기 위해, 인스펙터로 "끄고 싶은 컴포넌트들"을 지정하는 방식.
    /// </summary>
    public sealed class CombatFieldLock : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Disable while in combat")]
        [Tooltip("전투 중 비활성화할 Behaviour들(플레이어 이동, 상호작용, 전투 트리거 스크립트 등)을 넣어라.")]
        [SerializeField] private List<Behaviour> disableBehaviours = new();

        [Tooltip("전투 중 입력은 막되, 물리(리짓바디)가 남아 흔들리면 여기서 멈춤 처리.")]
        [SerializeField] private List<Rigidbody2D> freezeBodies2D = new();

        [Tooltip("전투 중 콜라이더 트리거(조우/상호작용)를 막고 싶으면 넣어라.")]
        [SerializeField] private List<Collider2D> disableColliders2D = new();

        private readonly Dictionary<Behaviour, bool> _prevBehaviourEnabled = new();
        private readonly Dictionary<Collider2D, bool> _prevColliderEnabled = new();
        private readonly Dictionary<Rigidbody2D, (Vector2 vel, float angVel, bool simulated)> _prevBody = new();

        private bool _locked;

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
            Lock();
        }

        private void HandleCombatEnded(CombatResult result)
        {
            Unlock();
        }

        public void Lock()
        {
            if (_locked) return;
            _locked = true;

            // 1) Behaviour disable
            _prevBehaviourEnabled.Clear();
            for (int i = 0; i < disableBehaviours.Count; i++)
            {
                var b = disableBehaviours[i];
                if (b == null) continue;

                _prevBehaviourEnabled[b] = b.enabled;
                b.enabled = false;
            }

            // 2) Collider disable (조우 트리거 등)
            _prevColliderEnabled.Clear();
            for (int i = 0; i < disableColliders2D.Count; i++)
            {
                var c = disableColliders2D[i];
                if (c == null) continue;

                _prevColliderEnabled[c] = c.enabled;
                c.enabled = false;
            }

            // 3) Rigidbody2D freeze (선택)
            _prevBody.Clear();
            for (int i = 0; i < freezeBodies2D.Count; i++)
            {
                var rb = freezeBodies2D[i];
                if (rb == null) continue;

                _prevBody[rb] = (rb.linearVelocity, rb.angularVelocity, rb.simulated);
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                // 물리 완전 정지 느낌 원하면 simulated=false (다만 애니/다른 시스템 영향 있을 수 있어 선택)
                // rb.simulated = false;
            }
        }

        public void Unlock()
        {
            if (!_locked) return;
            _locked = false;

            // 1) Behaviour restore
            foreach (var kv in _prevBehaviourEnabled)
            {
                if (kv.Key != null) kv.Key.enabled = kv.Value;
            }
            _prevBehaviourEnabled.Clear();

            // 2) Collider restore
            foreach (var kv in _prevColliderEnabled)
            {
                if (kv.Key != null) kv.Key.enabled = kv.Value;
            }
            _prevColliderEnabled.Clear();

            // 3) Rigidbody restore
            foreach (var kv in _prevBody)
            {
                var rb = kv.Key;
                if (rb == null) continue;

                rb.linearVelocity = kv.Value.vel;
                rb.angularVelocity = kv.Value.angVel;
                rb.simulated = kv.Value.simulated;
            }
            _prevBody.Clear();
        }
    }
}