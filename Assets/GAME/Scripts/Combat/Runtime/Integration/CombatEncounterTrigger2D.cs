// Assets/GAME/Scripts/Combat/Runtime/Integration/CombatEncounterTrigger2D.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.Adapters;
using Game.Core;
using Game.Player;

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

        [Header("Debug")]
        [SerializeField] private bool debugLog;

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
            {
                LogDebug($"Ignored trigger from {GetColliderName(other)} because this encounter is disarmed.");
                return;
            }

            LogDebug($"OnTriggerEnter2D other='{GetColliderName(other)}', tag='{GetColliderTag(other)}'.");

            GameObject playerRoot = ResolvePlayerRoot(other);
            if (playerRoot == null)
            {
                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because a player root could not be resolved.");
                Debug.LogWarning("[CombatEncounterTrigger2D] Player root could not be resolved from the trigger collider. Check PlayerInputController, CombatHpComponent, or Player tag placement.", this);
                return;
            }

            if (!CanStartInCurrentGameState())
                return;

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (entryPoint == null)
            {
                Debug.LogError("[CombatEncounterTrigger2D] EntryPoint is missing.");
                return;
            }

            if (entryPoint.ActiveStateMachine != null)
            {
                LogDebug($"Ignored trigger because an active combat state machine already exists. Phase={entryPoint.ActiveStateMachine.Phase}.");
                return;
            }

            if (enemyObject == null)
                enemyObject = gameObject;

            List<GameObject> allies = new List<GameObject>(1)
            {
                playerRoot
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

            bool started = entryPoint.StartCombatFromField(
                allies,
                enemies,
                startReason,
                initiativeSide,
                openingEffectOrNull
            );

            if (started)
            {
                Debug.Log(
                    $"[CombatEncounterTrigger2D] Combat started. " +
                    $"Allies={allies.Count}, Enemies={enemies.Count}, Reason={startReason}, Initiative={initiativeSide}"
                );
            }
            else
            {
                LogDebug(
                    "StartCombatFromField returned false. " +
                    $"GameState={GetCurrentGameStateText()}, " +
                    $"ActiveSessionNull={entryPoint.ActiveSession == null}, " +
                    $"ActiveStateMachineNull={entryPoint.ActiveStateMachine == null}, " +
                    $"Allies={allies.Count}, Enemies={enemies.Count}"
                );
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (ResolvePlayerRoot(other) == null)
                return;

            if (entryPoint != null && entryPoint.ActiveStateMachine == null)
                _armed = true;
        }

        private bool HasPlayerTag(Collider2D other)
        {
            if (other == null)
            {
                LogDebug("Ignored trigger because collider is null.");
                return false;
            }

            if (string.IsNullOrEmpty(playerTag))
            {
                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because PlayerTag is empty.");
                return false;
            }

            try
            {
                if (other.CompareTag(playerTag))
                    return true;

                LogDebug($"Ignored trigger from '{GetColliderName(other)}' because tag '{GetColliderTag(other)}' does not match PlayerTag '{playerTag}'.");
                return false;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"[CombatEncounterTrigger2D] PlayerTag '{playerTag}' is not defined. Trigger ignored. {exception.Message}", this);
                return false;
            }
        }

        private GameObject ResolvePlayerRoot(Collider2D other)
        {
            if (other == null)
                return null;

            PlayerInputController playerInput = other.GetComponentInParent<PlayerInputController>();
            if (playerInput != null)
                return playerInput.gameObject;

            CombatHpComponent hpComponent = other.GetComponentInParent<CombatHpComponent>();
            if (hpComponent != null)
                return hpComponent.gameObject;

            Transform root = other.transform != null ? other.transform.root : null;
            if (root != null && HasTag(root.gameObject, playerTag))
                return root.gameObject;

            return HasPlayerTag(other) ? other.gameObject : null;
        }

        private bool HasTag(GameObject target, string tagName)
        {
            if (target == null)
                return false;

            if (string.IsNullOrEmpty(tagName))
            {
                LogDebug($"Ignored '{target.name}' because PlayerTag is empty.");
                return false;
            }

            try
            {
                return target.CompareTag(tagName);
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"[CombatEncounterTrigger2D] PlayerTag '{tagName}' is not defined. Trigger ignored. {exception.Message}", this);
                return false;
            }
        }

        private bool CanStartInCurrentGameState()
        {
            if (GameStateMachine.Instance == null)
                return true;

            if (GameStateMachine.Instance.Is(GameState.Exploration))
                return true;

            LogDebug($"Ignored trigger because GameState is {GameStateMachine.Instance.Current}, not {GameState.Exploration}.");
            return false;
        }

        private string GetCurrentGameStateText()
        {
            return GameStateMachine.Instance != null ? GameStateMachine.Instance.Current.ToString() : "<none>";
        }

        private static string GetColliderName(Collider2D other)
        {
            return other != null && other.gameObject != null ? other.gameObject.name : "<null>";
        }

        private static string GetColliderTag(Collider2D other)
        {
            if (other == null || other.gameObject == null)
                return "<null>";

            try
            {
                return other.gameObject.tag;
            }
            catch (UnityException exception)
            {
                return $"<invalid tag: {exception.Message}>";
            }
        }

        private void LogDebug(string message)
        {
            if (!debugLog)
                return;

            Debug.Log($"[CombatEncounterTrigger2D] {message}", this);
        }
    }
}
