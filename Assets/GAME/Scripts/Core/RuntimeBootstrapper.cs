using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Interaction;
using Game.Reward;
using Game.UI;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Systems.Persona;
using Game.NonCombat.Party;
using Game.World.Exploration;
using Game.Story.Core;

namespace Game.Core
{
    public sealed class RuntimeBootstrapper : MonoBehaviour
    {
        private enum InitialStateMode
        {
            InferFromSceneName,
            Explicit
        }

        [SerializeField] private InitialStateMode initialStateMode = InitialStateMode.InferFromSceneName;
        [SerializeField] private GameState initialState = GameState.Exploration;
        [SerializeField] private bool applyInitialStateOnStart = true;
        [SerializeField] private bool createMissingCoreServices = true;
        [SerializeField] private bool logWarnings = true;
        [Tooltip("Assign only approved character/initial Party data. Empty retains existing scene and Test Hero compatibility.")]
        [SerializeField] private CharacterStartDefinitionSO characterStartDefinition;

        private int _initialStateAppliedSceneHandle = int.MinValue;

        /// <summary>
        /// Raised after a runtime bootstrapper has installed the scene's core/UI services and
        /// acknowledged that scene's initial-state policy. During save restoration,
        /// readiness is published while the restore coordinator retains Loading.
        /// </summary>
        public static event Action<Scene> OnInitialStateApplied;

        private void Awake()
        {
            BootstrapCoreServices(createMissingCoreServices, logWarnings, false);
        }

        private void Start()
        {
            if (applyInitialStateOnStart)
                ApplyInitialStateForActiveScene();
        }

        internal static void EnsureLoadedSceneCoreServices()
        {
            // Duplicate singleton components can destroy an authored bootstrap root and its
            // child services at end of frame. Recover through this owner before save restore.
            RuntimeBootstrapper bootstrapper = FindFirstObjectByType<RuntimeBootstrapper>();
            if (bootstrapper == null)
                bootstrapper = new GameObject("RuntimeBootstrapper").AddComponent<RuntimeBootstrapper>();
            bootstrapper.BootstrapCoreServices(bootstrapper.createMissingCoreServices, bootstrapper.logWarnings, false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapLoadedScene()
        {
            RuntimeBootstrapper existing = FindFirstObjectByType<RuntimeBootstrapper>();
            if (existing != null)
            {
                existing.BootstrapCoreServices(existing.createMissingCoreServices, existing.logWarnings, false);
                if (existing.applyInitialStateOnStart)
                    existing.ApplyInitialStateForActiveScene();
                return;
            }

            GameObject go = new GameObject("RuntimeBootstrapper");
            RuntimeBootstrapper bootstrapper = go.AddComponent<RuntimeBootstrapper>();
            bootstrapper.BootstrapCoreServices(true, true, true);

            bootstrapper.ApplyInitialStateForActiveScene();
        }

        public bool HasAppliedInitialStateFor(Scene scene)
        {
            return scene.IsValid() && _initialStateAppliedSceneHandle == scene.handle;
        }

        private void BootstrapCoreServices(bool createMissing, bool warn, bool compatibilityFallback)
        {
            FindOrCreate<GameStateMachine>("GameStateMachine", createMissing, warn);
            FindOrCreate<global::GameInputInstaller>("GameInputInstaller", createMissing, warn);
            FindOrCreate<GameFlowController>("GameFlowController", createMissing, warn);
            FindOrCreate<SceneFlowController>("SceneFlowController", createMissing, warn);
            FindOrCreate<StoryFlagManager>("StoryFlagManager", createMissing, warn);
            FindOrCreate<CurrencyWallet>("CurrencyWallet", createMissing, warn);
            FindOrCreate<InventoryService>("InventoryService", createMissing, warn);
            PartyRuntime party = FindOrCreate<PartyRuntime>("PartyRuntime", createMissing, warn);
            FindOrCreate<ExplorationResourceRuntime>("ExplorationResourceRuntime", createMissing, warn);
            FindOrCreate<PersistentConditionRuntime>("PersistentConditionRuntime", createMissing, warn);
            FindOrCreate<FeastService>("FeastService", createMissing, warn);
            CharacterProgressionService progression = FindOrCreate<CharacterProgressionService>("CharacterProgressionService", createMissing, warn);
            ConfigureCharacterServices(PartyRuntime.Instance != null ? PartyRuntime.Instance : party,
                CharacterProgressionService.Instance != null ? CharacterProgressionService.Instance : progression, warn);
            FindOrCreate<CharacterSkillSaveParticipant>("CharacterSkillSaveParticipant", createMissing, warn);
            FindOrCreate<PersonaStatusManager>("PersonaStatusManager", createMissing, warn);
            FindOrCreate<PersonaSaveAdapter>("PersonaSaveAdapter", createMissing, warn);
            InteractionRuntime interactionRuntime = FindOrCreate<InteractionRuntime>(
                "InteractionRuntime",
                createMissing,
                warn);
            InteractionRunner interactionRunner = FindOrCreate<InteractionRunner>(
                "InteractionRunner",
                createMissing,
                warn);
            interactionRunner?.ConfigureBootstrap(interactionRuntime, compatibilityFallback);
            FindOrCreate<SaveLoadService>("SaveLoadService", createMissing, warn);
            FindOrCreate<RewardService>("RewardService", createMissing, warn);
            FindOrCreate<GameUIRootController>("GameUIRootController", createMissing, warn);
            FindOrCreate<UIScreenRouter>("UIScreenRouter", createMissing, warn);
        }

        private void ConfigureCharacterServices(PartyRuntime party, CharacterProgressionService progression, bool warn)
        {
            if (characterStartDefinition == null || party == null || progression == null) return;
            // Validate both owners before either adopts authored configuration. Bootstrap never resets a session.
            if (!progression.CanConfigureStartDefinition(characterStartDefinition, out string message) ||
                !party.CanConfigureStartDefinition(characterStartDefinition, out message))
            {
                if (warn) Debug.LogWarning($"[RuntimeBootstrapper] Character startup configuration rejected: {message}", this);
                return;
            }
            progression.TryConfigureStartDefinition(characterStartDefinition, out _);
            party.TryConfigureStartDefinition(characterStartDefinition, out _);
        }

        private GameState ResolveInitialState()
        {
            return initialStateMode == InitialStateMode.Explicit
                ? initialState
                : ResolveInitialStateForScene(SceneManager.GetActiveScene().name);
        }

        internal static GameState ResolveInitialStateForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return GameState.Exploration;

            string normalized = sceneName.ToLowerInvariant();
            if (normalized.Contains("title"))
                return GameState.Title;

            if (normalized.Contains("loading"))
                return GameState.Loading;

            if (normalized.Contains("cutscene"))
                return GameState.Cutscene;

            return GameState.Exploration;
        }

        private static T FindOrCreate<T>(string objectName, bool createMissing, bool warn) where T : Component
        {
            T[] existing = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                if (warn && existing.Length > 1)
                    Debug.LogWarning($"[RuntimeBootstrapper] Multiple {typeof(T).Name} instances found. Existing singleton code should keep one active.");

                return existing[0];
            }

            if (!createMissing)
            {
                if (warn)
                    Debug.LogWarning($"[RuntimeBootstrapper] Missing {typeof(T).Name}. Assign or add {objectName} in the scene.");

                return null;
            }

            GameObject go = new GameObject(objectName);
            return go.AddComponent<T>();
        }

        private static void ApplyInitialState(GameState state)
        {
            if (GameFlowController.Instance != null)
                GameFlowController.Instance.RequestState(state, nameof(RuntimeBootstrapper));
            else
                GameStateMachine.Instance?.TrySetState(state, nameof(RuntimeBootstrapper));
        }

        private void ApplyInitialStateForActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || HasAppliedInitialStateFor(activeScene))
                return;

            SaveLoadService save = SaveLoadService.Instance;
            bool restoring = save != null && (save.CurrentOperationState == SaveLoadService.OperationState.WaitingForScene ||
                                             save.CurrentOperationState == SaveLoadService.OperationState.Restoring);
            // Publish scene readiness for adapters that defer until OnLoadCompleted, while
            // the restore coordinator alone releases Loading after participants restore.
            if (!restoring) ApplyInitialState(ResolveInitialState());
            _initialStateAppliedSceneHandle = activeScene.handle;
            OnInitialStateApplied?.Invoke(activeScene);
        }
    }
}
