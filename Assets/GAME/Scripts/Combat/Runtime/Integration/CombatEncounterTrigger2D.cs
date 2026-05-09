// Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;

namespace Game.Combat.Integration
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CombatEncounterTrigger2D : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private CombatEntryPoint entryPoint;

        [Header("Enemy")]
        [SerializeField] private GameObject enemyObject;
        [SerializeField] private CombatEncounterGroup encounterGroup;

        [Header("Opening / Initiative")]
        [SerializeField] private OpeningEffectSO openingEffectOrNull;
        [SerializeField] private StartReason startReason = StartReason.PlayerFirstHit;
        [SerializeField] private Side initiativeSide = Side.Allies;

        [Header("Filter")]
        [SerializeField] private string playerTag = "Player";

        private Collider2D _trigger;
        private bool _armed = true;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (enemyObject == null)
                enemyObject = gameObject;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_armed)
                return;

            if (entryPoint == null)
            {
                Debug.LogError("[CombatEncounterTrigger2D] EntryPoint is missing.");
                return;
            }

            if (entryPoint.ActiveStateMachine != null)
                return;

            if (!other.CompareTag(playerTag))
                return;

            List<GameObject> allies = new List<GameObject>(1)
            {
                other.gameObject
            };

            List<GameObject> enemies;

            if (encounterGroup != null)
            {
                enemies = encounterGroup.GetActiveEnemies();
            }
            else
            {
                enemies = new List<GameObject>(1) { enemyObject };
            }

            enemies.RemoveAll(go => go == null || !go.activeInHierarchy);

            if (enemies.Count == 0)
            {
                Debug.LogWarning("[CombatEncounterTrigger2D] No active enemies found.");
                return;
            }

            _armed = false;

            entryPoint.StartCombatFromField(
                allies,
                enemies,
                startReason,
                initiativeSide,
                openingEffectOrNull
            );

            Debug.Log(
                $"[CombatEncounterTrigger2D] StartCombatFromField called. " +
                $"Allies={allies.Count}, Enemies={enemies.Count}, Reason={startReason}, Initiative={initiativeSide}"
            );
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag))
                return;

            if (entryPoint != null && entryPoint.ActiveStateMachine == null)
                _armed = true;
        }
    }
}