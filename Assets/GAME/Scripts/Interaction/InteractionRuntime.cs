using System;
using System.Collections.Generic;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Interaction
{
    public sealed class InteractionRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static InteractionRuntime Instance { get; private set; }

        private readonly HashSet<string> _sessionConsumedIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeState> _persistentStates = new(StringComparer.Ordinal);

        public event Action StateRestored;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InteractionRuntime] Duplicate Production runtime rejected.", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsConsumed(string interactionId, InteractionUsePolicy usePolicy)
        {
            string id = InteractionIdentity.Normalize(interactionId);
            if (id == null)
                return false;

            return usePolicy switch
            {
                InteractionUsePolicy.OncePerSession => _sessionConsumedIds.Contains(id),
                InteractionUsePolicy.PersistentOnce => _persistentStates.TryGetValue(id, out RuntimeState state) && state.Consumed,
                _ => false
            };
        }

        public void MarkConsumed(string interactionId, InteractionUsePolicy usePolicy)
        {
            string id = InteractionIdentity.Normalize(interactionId);
            if (id == null)
                return;

            if (usePolicy == InteractionUsePolicy.OncePerSession)
            {
                _sessionConsumedIds.Add(id);
            }
            else if (usePolicy == InteractionUsePolicy.PersistentOnce)
            {
                GetOrCreate(id).Consumed = true;
            }
        }

        public bool TryGetResolvedOutcome(string interactionId, string actionId, out string outcomeId)
        {
            outcomeId = null;
            string key = ResolveOutcomeKey(interactionId, actionId);
            return key != null &&
                   _persistentStates.TryGetValue(InteractionIdentity.Normalize(interactionId), out RuntimeState state) &&
                   state.ResolvedOutcomes.TryGetValue(key, out outcomeId);
        }

        public void RememberResolvedOutcome(string interactionId, string actionId, string outcomeId)
        {
            string id = InteractionIdentity.Normalize(interactionId);
            string key = ResolveOutcomeKey(interactionId, actionId);
            string outcome = InteractionIdentity.Normalize(outcomeId);
            if (id == null || key == null || outcome == null)
                return;

            GetOrCreate(id).ResolvedOutcomes.TryAdd(key, outcome);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.world ??= new WorldSaveData();
            saveData.world.interactions ??= new List<InteractionStateSaveData>();
            saveData.world.interactions.Clear();

            List<string> ids = new(_persistentStates.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                RuntimeState state = _persistentStates[id];
                InteractionStateSaveData saved = new()
                {
                    interactionId = id,
                    consumed = state.Consumed
                };

                List<string> actionIds = new(state.ResolvedOutcomes.Keys);
                actionIds.Sort(StringComparer.Ordinal);
                for (int j = 0; j < actionIds.Count; j++)
                {
                    string actionId = actionIds[j];
                    saved.resolvedOutcomes.Add(new InteractionOutcomeSaveData
                    {
                        actionId = actionId,
                        outcomeId = state.ResolvedOutcomes[actionId]
                    });
                }

                saveData.world.interactions.Add(saved);
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _sessionConsumedIds.Clear();
            _persistentStates.Clear();
            List<InteractionStateSaveData> savedStates = saveData?.world?.interactions;
            if (savedStates != null)
            {
                for (int i = 0; i < savedStates.Count; i++)
                {
                    InteractionStateSaveData saved = savedStates[i];
                    string id = InteractionIdentity.Normalize(saved?.interactionId);
                    if (id == null)
                        continue;

                    RuntimeState state = GetOrCreate(id);
                    state.Consumed |= saved.consumed;
                    if (saved.resolvedOutcomes == null)
                        continue;

                    for (int j = 0; j < saved.resolvedOutcomes.Count; j++)
                    {
                        InteractionOutcomeSaveData outcome = saved.resolvedOutcomes[j];
                        string actionId = InteractionIdentity.Normalize(outcome?.actionId);
                        string outcomeId = InteractionIdentity.Normalize(outcome?.outcomeId);
                        if (actionId != null && outcomeId != null)
                            state.ResolvedOutcomes.TryAdd(actionId, outcomeId);
                    }
                }
            }

            InteractableObject[] fieldObjects = FindObjectsByType<InteractableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < fieldObjects.Length; i++)
                fieldObjects[i]?.BindRuntime(this);

            StateRestored?.Invoke();
        }

        internal void ResetSessionForTests()
        {
            _sessionConsumedIds.Clear();
        }

        internal static void ResetOwnershipForTests()
        {
            Instance = null;
        }

        private RuntimeState GetOrCreate(string interactionId)
        {
            if (!_persistentStates.TryGetValue(interactionId, out RuntimeState state))
            {
                state = new RuntimeState();
                _persistentStates.Add(interactionId, state);
            }

            return state;
        }

        private static string ResolveOutcomeKey(string interactionId, string actionId)
        {
            return InteractionIdentity.Normalize(interactionId) == null
                ? null
                : InteractionIdentity.Normalize(actionId);
        }

        private sealed class RuntimeState
        {
            internal bool Consumed;
            internal readonly Dictionary<string, string> ResolvedOutcomes = new(StringComparer.Ordinal);
        }
    }
}
