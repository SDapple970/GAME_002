using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters; // ✅ OpeningEffectSO 네임스페이스

namespace Game.Combat.Integration
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CombatEncounterTrigger2D : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Enemy (single)")]
        [SerializeField] private GameObject enemyObject; // 비워두면 이 오브젝트 사용

        [Header("Opening / Initiative")]
        [SerializeField] private OpeningEffectSO openingEffectOrNull;
        [SerializeField] private StartReason startReason = StartReason.PlayerFirstHit;
        [SerializeField] private Side initiativeSide = Side.Allies;

        [Header("Filter")]
        [SerializeField] private string playerTag = "Player";

        private Collider2D _col;
        private bool _armed = true;

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            _col.isTrigger = true;

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (enemyObject == null)
                enemyObject = transform.root.gameObject;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_armed) return;
            if (entryPoint == null) return;

            // 이미 전투 중이면 재진입 방지
            if (entryPoint.ActiveStateMachine != null) return;

            if (!other.CompareTag(playerTag)) return;

            var allies = new List<GameObject>(1) { other.gameObject };
            var enemies = new List<GameObject>(1) { enemyObject };

            entryPoint.StartCombatFromField(allies, enemies, startReason, initiativeSide, openingEffectOrNull);

            // 트리거 안에 서있을 때 전투 끝나자마자 재진입 방지
            _armed = false;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            // 전투 중이 아니면 재무장
            if (entryPoint != null && entryPoint.ActiveStateMachine == null)
                _armed = true;
        }
    }
}