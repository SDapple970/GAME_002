using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Interaction;
using Game.Player;
using Game.Quest;
using Game.Story;
using Game.Tutorial;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Debugging
{
    public sealed class VerticalSliceSceneValidator : MonoBehaviour
    {
        [SerializeField] private bool validateOnStart = true;
        [SerializeField] private bool validateOnHotkey = true;
        [SerializeField] private KeyCode validateKey = KeyCode.F8;
        [SerializeField] private bool logSuccessDetails = true;
        [SerializeField] private bool includeInactiveObjects = true;

        private int _errors;
        private int _warnings;

        private void Start()
        {
            if (validateOnStart)
                ValidateCurrentScene();
        }

        private void Update()
        {
            if (validateOnHotkey && UnityEngine.Input.GetKeyDown(validateKey))
                ValidateCurrentScene();
        }

        [ContextMenu("Validate Current Scene")]
        public void ValidateCurrentScene()
        {
            _errors = 0;
            _warnings = 0;

            string sceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[VerticalSliceValidator] ===== Scene Validation Start: {sceneName} =====", this);

            CheckRequired<Game.Core.GameStateMachine>("GameStateMachine");
            CheckRequired<global::GameInputInstaller>("GameInputInstaller");
            CheckRequired<ChapterProgressManager>("ChapterProgressManager");
            CheckRequired<QuestManager>("QuestManager");
            CheckRequired<SceneTravelService>("SceneTravelService");

            CheckRecommended<InteractionController>("InteractionController", "Interactions will not execute from the shared controller.");
            CheckRecommended<InteractionPromptUI>("InteractionPromptUI", "Interaction prompts will not be visible.");
            CheckRecommended<QuestTrackerUI>("QuestTrackerUI", "Quest progress will not be visible.");
            CheckRecommended<CombatEntryPoint>("CombatEntryPoint", "Tutorial battle entry will not be available.");
            CheckRecommended<RewardUIPanel>("RewardUIPanel", "Combat or field rewards may not be visible.");
            CheckRecommended<TutorialQuestCombatBridge>("TutorialQuestCombatBridge", "Combat win will not update the tutorial quest automatically.");

            CheckPlayer();
            CheckSpawnPoints();
            CheckInteractables();

            Debug.Log($"[VerticalSliceValidator] Result: errors={_errors}, warnings={_warnings}", this);
        }

        private void CheckRequired<T>(string label) where T : Object
        {
            T found = FindFirst<T>();
            if (found == null)
                LogError($"{label} missing.");
            else
                LogOk($"{label} found.");
        }

        private void CheckRecommended<T>(string label, string consequence) where T : Object
        {
            T found = FindFirst<T>();
            if (found == null)
                LogWarn($"{label} missing. {consequence}");
            else
                LogOk($"{label} found.");
        }

        private void CheckPlayer()
        {
            GameObject player = FindPlayerByTag();
            if (player == null)
            {
                LogError("Player tag object missing. Scene travel spawn will fail.");
                return;
            }

            LogOk($"Player tag object found: {GetObjectPath(player.transform)}");

            if (player.GetComponent<Rigidbody2D>() == null)
                LogWarn("Player Rigidbody2D missing. Movement or spawn velocity reset may fail.");
            else
                LogOk("Player Rigidbody2D found.");

            if (player.GetComponent<OverworldPlayerController>() == null)
                LogWarn("Player OverworldPlayerController missing. Field input may not work.");
            else
                LogOk("Player OverworldPlayerController found.");

            if (player.GetComponent<global::PlayerMotor2D>() == null)
                LogWarn("Player PlayerMotor2D missing. Field movement may not work.");
            else
                LogOk("Player PlayerMotor2D found.");
        }

        private GameObject FindPlayerByTag()
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    return player;
            }
            catch (UnityException ex)
            {
                LogError($"Player tag lookup failed. Is the 'Player' tag defined? {ex.Message}");
                return null;
            }

            if (!includeInactiveObjects)
                return null;

            Transform[] transforms = FindAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                    continue;

                try
                {
                    if (candidate.CompareTag("Player"))
                        return candidate.gameObject;
                }
                catch (UnityException)
                {
                    return null;
                }
            }

            return null;
        }

        private void CheckSpawnPoints()
        {
            SceneSpawnPoint[] spawnPoints = FindAll<SceneSpawnPoint>();
            if (spawnPoints.Length == 0)
            {
                LogWarn("SceneSpawnPoint missing. Scene travel spawn will use no target position.");
                return;
            }

            LogOk($"SceneSpawnPoint count={spawnPoints.Length}.");

            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SceneSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                    continue;

                string id = spawnPoint.SpawnPointId;
                Debug.Log($"[VerticalSliceValidator] SpawnPoint: id='{id}', object={GetObjectPath(spawnPoint.transform)}", spawnPoint);

                if (string.IsNullOrWhiteSpace(id))
                {
                    LogWarn($"SceneSpawnPoint has empty id: {GetObjectPath(spawnPoint.transform)}");
                    continue;
                }

                if (!ids.Add(id))
                    LogWarn($"Duplicate SceneSpawnPoint id detected: {id}");
            }
        }

        private void CheckInteractables()
        {
            InteractableObject[] interactables = FindAll<InteractableObject>();
            if (interactables.Length == 0)
            {
                LogWarn("InteractableObject missing. Office/Tutorial interactions cannot be started.");
                return;
            }

            LogOk($"InteractableObject count={interactables.Length}.");

            for (int i = 0; i < interactables.Length; i++)
            {
                InteractableObject interactable = interactables[i];
                if (interactable == null)
                    continue;

                Collider2D collider = interactable.GetComponent<Collider2D>();
                if (collider == null)
                {
                    LogWarn($"InteractableObject Collider2D missing: {GetObjectPath(interactable.transform)}");
                }
                else if (!collider.isTrigger)
                {
                    LogWarn($"InteractableObject Collider2D is not Trigger: {GetObjectPath(interactable.transform)}");
                }

                int? eventCount = TryGetInteractionEventCount(interactable);
                if (eventCount.HasValue && eventCount.Value == 0)
                    LogWarn($"InteractableObject events list is empty: {GetObjectPath(interactable.transform)}");
            }
        }

        private static int? TryGetInteractionEventCount(InteractableObject interactable)
        {
            FieldInfo field = typeof(InteractableObject).GetField("events", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return null;

            object value = field.GetValue(interactable);
            if (value is ICollection collection)
                return collection.Count;

            return null;
        }

        private T FindFirst<T>() where T : Object
        {
            T[] objects = FindAll<T>();
            return objects.Length > 0 ? objects[0] : null;
        }

        private T[] FindAll<T>() where T : Object
        {
            FindObjectsInactive inactiveMode = includeInactiveObjects
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            return Object.FindObjectsByType<T>(inactiveMode, FindObjectsSortMode.None);
        }

        private void LogOk(string message)
        {
            if (logSuccessDetails)
                Debug.Log($"[VerticalSliceValidator] OK: {message}", this);
        }

        private void LogWarn(string message)
        {
            _warnings++;
            Debug.LogWarning($"[VerticalSliceValidator] WARN: {message}", this);
        }

        private void LogError(string message)
        {
            _errors++;
            Debug.LogError($"[VerticalSliceValidator] ERROR: {message}", this);
        }

        private static string GetObjectPath(Transform transform)
        {
            if (transform == null)
                return "NULL";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
