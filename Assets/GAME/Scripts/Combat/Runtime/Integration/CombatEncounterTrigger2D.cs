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
        private bool _requestInProgress;
        private int _lastRequestFrame = -1;

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
            if (!_armed || _requestInProgress || _lastRequestFrame == Time.frameCount)
            {
                LogDebug($"Ignored trigger from {GetColliderName(other)} because this encounter is already processing or disarmed.");
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

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();

            if (entryPoint == null)
            {
                Debug.LogError("[CombatEncounterTrigger2D] EntryPoint is missing.");
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

            CombatStartRequest request = CreateEncounterRequest(allies, enemies);
            bool started;
            _requestInProgress = true;
            _lastRequestFrame = Time.frameCount;
            try
            {
                started = entryPoint.StartCombat(request);
            }
            finally
            {
                _requestInProgress = false;
            }

            if (started)
            {
                _armed = false;
                Debug.Log(
                    $"[CombatEncounterTrigger2D] Combat started. " +
                    $"Allies={allies.Count}, Enemies={enemies.Count}, Reason={startReason}, Initiative={initiativeSide}"
                );
            }
            else
            {
                _armed = true;
                LogDebug(
                    "StartCombat returned false; encounter remains armed. " +
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

        private CombatStartRequest CreateEncounterRequest(List<GameObject> allies, List<GameObject> enemies)
        {
            CombatStartRequest request = new CombatStartRequest(
                startReason,
                initiativeSide,
                0,
                -1,
                openingEffectOrNull
            );

            AddValidObjects(request.AllyFieldObjects, allies);
            AddValidObjects(request.EnemyFieldObjects, enemies);
            return request;
        }

        private static void AddValidObjects(List<GameObject> destination, List<GameObject> source)
        {
            if (destination == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy && !destination.Contains(candidate))
                    destination.Add(candidate);
            }
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
