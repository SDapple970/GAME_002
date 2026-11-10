using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.NonCombat.Save;
using Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace Game.Core
{
    public sealed class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }
        private GameSaveData _dungeonStart;
        private Dictionary<string, (int hp, int maxHp)> _dungeonStartHp;
        private bool _restartInProgress;
        private bool _sceneLoadInProgress;
        private int _loadGeneration;
        internal bool IsRestartInProgress => _restartInProgress;
        internal bool IsSceneLoadInProgress => _sceneLoadInProgress;

        public bool CanRestartDungeon => !_restartInProgress && !_sceneLoadInProgress && _dungeonStart != null &&
            !HasUnsupportedPersistentOwners() && _dungeonStart.header.activeSceneId == SceneManager.GetActiveScene().name;

        internal void InvalidateDungeonStartSnapshot()
        {
            _dungeonStart = null;
            _dungeonStartHp = null;
        }

        internal bool TryRestartDungeon()
        {
            CombatEntryPoint entry = FindFirstObjectByType<CombatEntryPoint>();
            if (!CanRestartDungeon || _sceneLoadInProgress || SaveLoadService.Instance == null ||
                !Application.CanStreamedLevelBeLoaded(_dungeonStart.header.activeSceneId) ||
                (entry != null && entry.ActiveSession != null)) return false;
            _restartInProgress = true;
            LoadSceneForRestore(_dungeonStart.header.activeSceneId, OnDungeonReloaded);
            return true;
        }

        private void OnDungeonReloaded(bool success)
        {
            if (success && _dungeonStart != null && TryResolveHp(out Dictionary<string, CombatHpComponent> targets) &&
                _dungeonStartHp.Count == targets.Count && _dungeonStartHp.Keys.All(targets.ContainsKey))
            {
                // Restore from a copy so repeated attempts cannot mutate the baseline.
                GameSaveData copy = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(_dungeonStart));
                if (SaveLoadService.Instance != null && SaveLoadService.Instance.TryRestoreDungeonSnapshot(copy))
                {
                    foreach (var item in _dungeonStartHp)
                    {
                        CombatHpComponent hp = targets[item.Key];
                        hp.MaxHP = item.Value.maxHp;
                        hp.HP = item.Value.hp;
                    }
                    _restartInProgress = false;
                    GameFlowController.Instance?.EnterExploration();
                    return;
                }
            }
            _restartInProgress = false;
            InvalidateDungeonStartSnapshot();
            Debug.LogError("[SceneFlowController] Dungeon restart restore failed; returning to Title when available.", this);
            if (Application.CanStreamedLevelBeLoaded("TitleScene")) LoadScene("TitleScene");
        }

        private void CaptureDungeonStart()
        {
            InvalidateDungeonStartSnapshot();
            CombatEntryPoint entry = FindFirstObjectByType<CombatEntryPoint>();
            if (FindFirstObjectByType<DungeonCompletionFlow>() == null || SaveLoadService.Instance == null ||
                (entry != null && entry.ActiveSession != null) ||
                (GameStateMachine.Instance != null && GameStateMachine.Instance.IsCombatState()) ||
                HasUnsupportedPersistentOwners() ||
                !TryResolveHp(out Dictionary<string, CombatHpComponent> targets)) return;
            try
            {
                GameSaveData data = SaveLoadService.Instance.CaptureGameSaveDataSnapshot();
                if (data.location == null || !data.location.hasPositionFallback) return;
                // A retry uses the exact start position, not the nearest-spawn save heuristic.
                data.header.playerSpawnId = string.Empty;
                _dungeonStart = data;
                _dungeonStartHp = targets.ToDictionary(pair => pair.Key, pair => (pair.Value.HP, pair.Value.MaxHP));
            }
            catch (Exception exception)
            {
                InvalidateDungeonStartSnapshot();
                Debug.LogError($"[SceneFlowController] Dungeon start snapshot failed: {exception}", this);
            }
        }

        private static bool TryResolveHp(out Dictionary<string, CombatHpComponent> targets)
        {
            targets = new Dictionary<string, CombatHpComponent>();
            Scene scene = SceneManager.GetActiveScene();
            foreach (CombatHpComponent hp in FindObjectsByType<CombatHpComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hp.gameObject.scene != scene)
                {
                    // Persistent field actors need an identity/restore contract before a rewind is safe.
                    if (hp.gameObject.scene.name == "DontDestroyOnLoad") return false;
                    continue;
                }
                string path = string.Empty;
                for (Transform current = hp.transform; current != null; current = current.parent)
                    path = "/" + current.GetSiblingIndex() + ":" + current.name + path;
                if (targets.ContainsKey(path)) return false;
                targets.Add(path, hp);
            }
            return true;
        }

        private static bool HasUnsupportedPersistentOwners()
        {
            // These compatibility owners maintain independent mutable state outside save participants.
            return Game.Mission.MissionManager.Instance != null || Game.Quest.QuestManager.Instance != null ||
                Game.Search.SearchRewardManager.Instance != null || Game.Story.ChapterProgressManager.Instance != null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneName)
        {
            LoadScene(sceneName, null);
        }

        public void LoadScene(string sceneName, Action<bool> sceneLoaded)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || _restartInProgress)
            {
                Debug.LogWarning("[SceneFlowController] Scene name is empty.", this);
                sceneLoaded?.Invoke(false);
                return;
            }

            SaveLoadService.Instance?.CaptureEncounterStateBeforeSceneChange();
            InvalidateDungeonStartSnapshot();
            _sceneLoadInProgress = true;
            StartCoroutine(Co_LoadScene(sceneName, true, sceneLoaded, ++_loadGeneration));
        }

        public void LoadSceneForRestore(string sceneName, Action<bool> completed)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                completed?.Invoke(false);
                return;
            }

            _sceneLoadInProgress = true;
            StartCoroutine(Co_LoadScene(sceneName, false, completed, ++_loadGeneration));
        }

        private IEnumerator Co_LoadScene(string sceneName, bool enterExploration, Action<bool> completed, int generation)
        {
            if (GameFlowController.Instance != null)
                GameFlowController.Instance.BeginLoading();
            else
                GameStateMachine.Instance?.TrySetState(GameState.Loading, nameof(SceneFlowController));

            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception exception) { Debug.LogError($"[SceneFlowController] Scene load failed: {exception.Message}", this); }
            if (operation == null)
            {
                Debug.LogError($"[SceneFlowController] Failed to load scene: {sceneName}", this);
                _sceneLoadInProgress = false;
                if (!_restartInProgress && GameFlowController.Instance != null)
                    GameFlowController.Instance.EnterExploration();
                else if (!_restartInProgress)
                    GameStateMachine.Instance?.TrySetState(GameState.Exploration, nameof(SceneFlowController));
                completed?.Invoke(false);
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            // Allow scene Awake/Start work to register scene-authored restore
            // participants before the completion callback runs.
            yield return null;

            if (generation != _loadGeneration)
            {
                completed?.Invoke(false);
                yield break;
            }

            RuntimeBootstrapper.EnsureLoadedSceneCoreServices();

            if (enterExploration)
                SaveLoadService.Instance?.ApplyEncounterStateToLoadedScene();

            completed?.Invoke(true);

            if (generation != _loadGeneration) yield break;

            if (enterExploration)
                CaptureDungeonStart();

            _sceneLoadInProgress = false;

            // A scene whose bootstrap root is removed together with duplicate Core
            // services still needs the same initial-state policy as RuntimeBootstrapper.
            // In particular, loading Title must not be forced into Exploration.
            if (enterExploration && GameStateMachine.Instance?.Current == GameState.Loading)
            {
                GameState initialState = RuntimeBootstrapper.ResolveInitialStateForScene(sceneName);
                if (GameFlowController.Instance != null)
                    GameFlowController.Instance.RequestState(initialState, nameof(SceneFlowController));
                else
                    GameStateMachine.Instance?.TrySetState(initialState, nameof(SceneFlowController));
            }

        }
    }
}
