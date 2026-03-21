// GAME/Scripts/Battle/FieldEnemy.cs
using System;
using UnityEngine;
using Game.Common;

namespace Game.Battle
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldEnemy : MonoBehaviour, IDamageable
    {
        public static event Action<BattleTransitionRequest> OnBattleRequested;

        [Header("Encounter Settings")]
        [SerializeField] private string battleSceneName = "Battle";

        [Header("AI Settings")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private float aggroRange = 5f;
        [SerializeField] private float moveSpeed = 2.5f;

        private bool _isEncounterTriggered;

        private void Awake()
        {
            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTarget = player.transform;
            }
        }

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Update()
        {
            if (_isEncounterTriggered || playerTarget == null)
                return;

            float distance = Vector2.Distance(transform.position, playerTarget.position);

            if (distance <= aggroRange)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerTarget.position,
                    moveSpeed * Time.deltaTime
                );
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isEncounterTriggered)
                return;

            if (!other.CompareTag("Player"))
                return;

            TriggerBattle(EncounterAdvantage.EnemyFirst);
        }

        public void TakeDamage(int amount)
        {
            if (_isEncounterTriggered)
                return;

            TriggerBattle(EncounterAdvantage.PlayerFirst);
        }

        private void TriggerBattle(EncounterAdvantage advantage)
        {
            _isEncounterTriggered = true;
            Debug.Log($"[FieldEnemy] 전투 발생! 어드밴티지: {advantage}");

            OnBattleRequested?.Invoke(
                new BattleTransitionRequest(transform.position, battleSceneName, advantage)
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
#endif
    }
}