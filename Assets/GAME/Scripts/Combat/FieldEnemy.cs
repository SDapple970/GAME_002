// GAME/Scripts/Battle/FieldEnemy.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;

namespace Game.Battle
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldEnemy : MonoBehaviour, Game.Common.IDamageable
    {
        [Header("Combat Entry")]
        [SerializeField] private CombatEntryPoint combatEntryPoint;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private OpeningEffectSO openingEffectOrNull;

        [Header("Encounter Settings")]
        [SerializeField] private StartReason touchStartReason = StartReason.PlayerGotHit;
        [SerializeField] private StartReason hitStartReason = StartReason.PlayerFirstHit;

        [Header("AI Settings")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private float aggroRange = 5f;
        [SerializeField] private float moveSpeed = 2.5f;

        private bool _isEncounterTriggered;

        private void Awake()
        {
            if (combatEntryPoint == null)
                combatEntryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
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

            if (!other.CompareTag(playerTag))
                return;

            StartCombat(other.gameObject, Side.Enemies, touchStartReason);
        }

        public void TakeDamage(int amount)
        {
            if (_isEncounterTriggered)
                return;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
                return;

            StartCombat(player, Side.Allies, hitStartReason);
        }

        private void StartCombat(GameObject playerObject, Side initiativeSide, StartReason reason)
        {
            if (_isEncounterTriggered)
                return;

            if (combatEntryPoint == null || playerObject == null)
            {
                Debug.LogError("[FieldEnemy] CombatEntryPoint or Player is missing.");
                return;
            }

            if (combatEntryPoint.ActiveStateMachine != null)
                return;

            _isEncounterTriggered = true;

            List<GameObject> allies = new List<GameObject>(1) { playerObject };
            List<GameObject> enemies = new List<GameObject>(1) { gameObject };

            bool started = combatEntryPoint.StartCombatFromField(
                allyFieldObjects: allies,
                enemyFieldObjects: enemies,
                reason: reason,
                initiativeSide: initiativeSide,
                openingEffectOrNull: openingEffectOrNull
            );

            if (started)
                Debug.Log($"[FieldEnemy] Combat started. reason={reason}, initiative={initiativeSide}");
        }
    }
}
