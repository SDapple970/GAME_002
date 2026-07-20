using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.NonCombat.Save;
using Game.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public sealed class SaveLoadService : MonoBehaviour
    {
        public enum OperationState { Idle, Capturing, Writing, Reading, Migrating, WaitingForScene, Restoring, Completed, Failed }

        public static SaveLoadService Instance { get; private set; }
        public event Action<bool, string> OnLoadCompleted;

        [SerializeField] private SaveManager saveManager;
        [SerializeField] private Transform player;
        [SerializeField] private string saveFileName = "game_save.json";

        private AtomicSaveStorage _storage;
        private OperationState _operationState;
        private int _operationToken;
        private bool _restoreHadErrors;

        public OperationState CurrentOperationState => _operationState;
        public string PrimarySavePath => Storage.PrimaryPath;
        public string BackupSavePath => Storage.BackupPath;

        private AtomicSaveStorage Storage => _storage ??= new AtomicSaveStorage(Path.Combine(Application.persistentDataPath, saveFileName));

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _operationToken++;
            _operationState = OperationState.Idle;
        }

        public void Save() => TrySave(out _);
        public void Load() => TryLoad(out _);

        public bool TrySave(out string message)
        {
            if (!TryBegin(false, out message)) return false;
            try
            {
                _operationState = OperationState.Capturing;
                GameSaveData snapshot = CaptureGameSaveDataSnapshot();
                GameSaveDataValidator.Normalize(snapshot);
                if (!GameSaveDataValidator.TryValidate(snapshot, out message)) return Fail(message);
                _operationState = OperationState.Writing;
                if (!Storage.TryWrite(SaveSerializer.ToJson(snapshot), out message)) return Fail(message);
                return Complete($"Saved {PrimarySavePath}.");
            }
            catch (Exception exception) { return Fail($"Save failed: {exception.Message}"); }
        }

        public bool TryLoad(out string message)
        {
            if (!TryBegin(true, out message)) return false;
            _operationState = OperationState.Reading;
            if (!TryReadValidSnapshot(out GameSaveData snapshot, out bool backup, out message)) return Fail(message);
            string targetScene = snapshot.header.activeSceneId;
            if (!string.IsNullOrWhiteSpace(targetScene) && targetScene != SceneManager.GetActiveScene().name)
            {
                if (!Application.CanStreamedLevelBeLoaded(targetScene)) return Fail($"Saved scene '{targetScene}' is not available in Build Settings.");
                SceneFlowController flow = SceneFlowController.Instance;
                if (flow == null) return Fail("SceneFlowController is missing.");
                int token = ++_operationToken;
                _operationState = OperationState.WaitingForScene;
                flow.LoadSceneForRestore(targetScene, success => HandleSceneLoadCompleted(token, success, snapshot, backup));
                message = "Load accepted; waiting for scene.";
                return true;
            }

            return RestoreAndFinish(snapshot, backup, out message);
        }

        public GameSaveData CaptureGameSaveDataSnapshot()
        {
            GameSaveData data = new();
            data.header.savedAtUtc = DateTime.UtcNow.ToString("O");
            data.header.applicationVersion = Application.version;
            data.header.activeSceneId = SceneManager.GetActiveScene().name;
            CaptureLocation(data);
            foreach (ISaveDataProvider provider in Discover<ISaveDataProvider>()) provider.CaptureSaveData(data);
            return data;
        }

        public void RestoreGameSaveDataSnapshot(GameSaveData saveData)
        {
            if (saveData == null) return;
            _restoreHadErrors = false;
            foreach (ISaveDataConsumer consumer in Discover<ISaveDataConsumer>())
            {
                try { consumer.RestoreSaveData(saveData); }
                catch (Exception exception) { _restoreHadErrors = true; Debug.LogError($"[SaveLoadService] Restore consumer {consumer.GetType().FullName} failed: {exception}", this); }
            }
            RestoreLocation(saveData);
        }

        internal void SetStoragePathForTests(string primaryPath) => _storage = new AtomicSaveStorage(primaryPath);
        internal void ResetOperationForTests() { _operationToken++; _operationState = OperationState.Idle; }

        private bool TryBegin(bool load, out string reason)
        {
            if (_operationState != OperationState.Idle) { reason = $"Another save/load operation is {_operationState}."; return false; }
            if (!(load ? CanLoadNow() : CanSaveNow())) { reason = $"{(load ? "Load" : "Save")} blocked in state {GameStateMachine.Instance?.Current}."; return false; }
            reason = null; return true;
        }

        private bool TryReadValidSnapshot(out GameSaveData data, out bool backup, out string error)
        {
            data = null; backup = false;
            if (TryReadPath(Storage.PrimaryPath, out data, out error)) return true;
            string primaryError = error;
            if (TryReadPath(Storage.BackupPath, out data, out error)) { backup = true; return true; }
            error = !File.Exists(Storage.PrimaryPath) && !File.Exists(Storage.BackupPath) ? "No save file exists." : $"Primary invalid: {primaryError}; backup invalid: {error}";
            return false;
        }

        private bool TryReadPath(string path, out GameSaveData data, out string error)
        {
            data = null; error = null;
            if (!File.Exists(path)) { error = "File not found."; return false; }
            try
            {
                _operationState = OperationState.Migrating;
                return GameSaveDataMigrator.TryMigrate(File.ReadAllText(path), out data, out _, out error);
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private void HandleSceneLoadCompleted(int token, bool success, GameSaveData data, bool backup)
        {
            if (token != _operationToken || _operationState != OperationState.WaitingForScene) return;
            if (!success) { Fail("Target scene load failed."); OnLoadCompleted?.Invoke(false, "Target scene load failed."); return; }
            RestoreAndFinish(data, backup, out _);
        }

        private bool RestoreAndFinish(GameSaveData data, bool backup, out string message)
        {
            if (GameFlowController.Instance != null) GameFlowController.Instance.BeginLoading();
            else GameStateMachine.Instance?.TrySetState(GameState.Loading, nameof(SaveLoadService));
            _operationState = OperationState.Restoring;
            RestoreGameSaveDataSnapshot(data);
            if (GameFlowController.Instance != null) GameFlowController.Instance.EnterExploration();
            else GameStateMachine.Instance?.TrySetState(GameState.Exploration, nameof(SaveLoadService));
            message = backup ? "Loaded from backup." : "Loaded primary save.";
            if (_restoreHadErrors)
            {
                message += " One or more participants failed; load is partial.";
                _operationState = OperationState.Failed;
                Debug.LogWarning($"[SaveLoadService] {message}", this);
                _operationState = OperationState.Idle;
                OnLoadCompleted?.Invoke(false, message);
                return false;
            }
            Complete(message);
            OnLoadCompleted?.Invoke(true, message);
            return true;
        }

        private static List<T> Discover<T>()
        {
            List<MonoBehaviour> ordered = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item != null && item is T)
                .OrderBy(item => RestorePriority(item.GetType()))
                .ThenBy(item => item.GetType().FullName, StringComparer.Ordinal)
                .ThenBy(item => item.name, StringComparer.Ordinal)
                .ToList();
            HashSet<Type> claimedTypes = new();
            List<T> result = new();
            foreach (MonoBehaviour item in ordered)
            {
                bool collectionParticipant = item.GetType().FullName != null && item.GetType().FullName.Contains("CombatEncounter");
                if (!collectionParticipant && !claimedTypes.Add(item.GetType()))
                {
                    Debug.LogWarning($"[SaveLoadService] Duplicate save participant type ignored: {item.GetType().FullName} on '{item.name}'.");
                    continue;
                }
                result.Add((T)(object)item);
            }
            return result;
        }

        private static int RestorePriority(Type type)
        {
            string name = type.FullName ?? type.Name;
            if (name.Contains("StoryProgress") || name.Contains("StoryFlag") || name.Contains("Chapter")) return 100;
            if (name.Contains("Inventory") || name.Contains("Currency")) return 200;
            if (name.Contains("Persona")) return 300;
            if (name.Contains("QuestRuntime")) return 400;
            if (name.Contains("DemoMission")) return 500;
            if (name.Contains("RewardService")) return 600;
            if (name.Contains("Encounter")) return 700;
            return 800;
        }

        private void CaptureLocation(GameSaveData data)
        {
            SceneSpawnPoint[] points = FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform target = ResolvePlayer();
            if (target == null) return;
            data.location.hasPositionFallback = true;
            data.location.positionX = target.position.x; data.location.positionY = target.position.y; data.location.positionZ = target.position.z;
            SceneSpawnPoint nearest = points.Where(point => point != null && !string.IsNullOrWhiteSpace(point.SpawnPointId)).OrderBy(point => Vector3.SqrMagnitude(point.transform.position - target.position)).FirstOrDefault();
            data.header.playerSpawnId = nearest != null && Vector3.SqrMagnitude(nearest.transform.position - target.position) <= 0.0625f
                ? nearest.SpawnPointId
                : string.Empty;
        }

        private void RestoreLocation(GameSaveData data)
        {
            Transform target = ResolvePlayer(); if (target == null || data?.location == null) return;
            SceneSpawnPoint point = FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(item => item != null && item.SpawnPointId == data.header.playerSpawnId);
            if (point != null) target.position = point.transform.position;
            else if (data.location.hasPositionFallback) target.position = new Vector3(data.location.positionX, data.location.positionY, data.location.positionZ);
            Rigidbody2D body = target.GetComponent<Rigidbody2D>(); if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            return found != null ? found.transform : null;
        }

        private static bool CanSaveNow() => IsAllowed(false);
        private static bool CanLoadNow() => IsAllowed(true);
        private static bool IsAllowed(bool load)
        {
            if (GameStateMachine.Instance == null) return true;
            GameState current = GameStateMachine.Instance.Current;
            if (current == GameState.Exploration) return true;
            if (load && current == GameState.Title) return true;
            return current == GameState.Paused && GameStateMachine.Instance.Previous == GameState.Exploration;
        }

        private bool Complete(string message) { _operationState = OperationState.Completed; Debug.Log($"[SaveLoadService] {message}", this); _operationState = OperationState.Idle; return true; }
        private bool Fail(string message) { _operationState = OperationState.Failed; Debug.LogWarning($"[SaveLoadService] {message}", this); _operationState = OperationState.Idle; return false; }
    }
}
