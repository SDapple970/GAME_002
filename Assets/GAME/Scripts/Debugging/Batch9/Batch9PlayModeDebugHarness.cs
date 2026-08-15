using Game.Core;
using Game.NonCombat.Inventory;
using Game.World.Exploration;
using UnityEngine;

namespace Game.Debugging
{
    public sealed class Batch9PlayModeDebugHarness : MonoBehaviour
    {
        private const string LogPrefix = "[Batch9Test]";

        [Header("Test Inputs")]
        [SerializeField] private string targetCharacterId = "hero";
        [SerializeField] private PersistentConditionDefinitionSO diseaseDefinition;
        [SerializeField] private PersistentConditionDefinitionSO quirkDefinition;
        [SerializeField] private string feastItemId = "ration";
        [Min(1), SerializeField] private int feastItemCount = 1;
        [Min(1), SerializeField] private int hungerRestoreAmount = 5;

        [Header("Load Test Mutation")]
        [SerializeField] private int loadTestShiningChange = 1;
        [SerializeField] private int loadTestHungerChange = 1;

        [Header("Optional Runtime References")]
        [SerializeField] private ExplorationResourceRuntime explorationResources;
        [SerializeField] private PersistentConditionRuntime persistentConditions;
        [SerializeField] private FeastService feastService;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private SaveLoadService saveLoadService;

        [Header("Current State (Play Mode)")]
        [SerializeField] private int currentShining;
        [SerializeField] private int currentHunger;
        [SerializeField] private string currentCharacterId;
        [SerializeField] private string currentDiseaseId;
        [SerializeField] private string currentQuirkId;
        [SerializeField] private string currentFeastItemId;
        [SerializeField] private int currentFeastItemCount;
        [SerializeField] private int currentHungerRestoreAmount;
        [TextArea, SerializeField] private string lastResult = "Not Run";

        private ExplorationResourceRuntime _subscribedResources;
        private PersistentConditionRuntime _subscribedConditions;
        private InventoryService _subscribedInventory;
        private SaveLoadService _subscribedSaveLoad;

        private void OnEnable()
        {
            if (Application.isPlaying)
                ResolveServices();
        }

        private void Start()
        {
            ResolveServices();
            RefreshInspectorState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        [ContextMenu("Batch 9/Shining/Add Shining 5")]
        public void AddShining5()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Shining Add", reason); return; }
            int before = explorationResources.Shining;
            bool result = explorationResources.TryAddShining(5);
            RefreshInspectorState();
            Report($"Shining Add: {before} -> {currentShining} / Result={result}");
        }

        [ContextMenu("Batch 9/Shining/Spend Shining 2")]
        public void SpendShining2() => SpendShining(2);

        [ContextMenu("Batch 9/Shining/Spend Shining 10")]
        public void SpendShining10() => SpendShining(10);

        [ContextMenu("Batch 9/Hunger/Add Hunger 4")]
        public void AddHunger4()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Hunger Add", reason); return; }
            int before = explorationResources.Hunger;
            bool result = explorationResources.TryChangeHunger(4);
            RefreshInspectorState();
            Report($"Hunger Add: {before} -> {currentHunger} / Result={result}");
        }

        [ContextMenu("Batch 9/Feast/Try Feast")]
        public void TryFeast()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Feast", reason); return; }
            int itemBefore = inventoryService.GetCount(feastItemId);
            int hungerBefore = explorationResources.Hunger;
            FeastResult result = feastService.TryFeast(new FeastRequest(feastItemId, feastItemCount, hungerRestoreAmount));
            RefreshInspectorState();
            Report($"Feast: item='{feastItemId}' {itemBefore} -> {currentFeastItemCount}, Hunger {hungerBefore} -> {currentHunger} / Result={result.Status}");
        }

        [ContextMenu("Batch 9/Feast/Add Test Feast Item")]
        public void AddTestFeastItem()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Feast Item Add", reason); return; }
            int before = inventoryService.GetCount(feastItemId);
            InventoryMutationResult result = inventoryService.TryAddItem(feastItemId, feastItemCount);
            RefreshInspectorState();
            Report($"Feast Item Add: '{feastItemId}' {before} -> {currentFeastItemCount} / Result={result.Status}, Applied={result.AppliedAmount}");
        }

        [ContextMenu("Batch 9/Conditions/Acquire Disease")]
        public void AcquireDisease() => AcquireCondition("Disease Acquire", diseaseDefinition, PersistentConditionCategory.Disease);

        [ContextMenu("Batch 9/Conditions/Acquire Disease Again")]
        public void AcquireDiseaseAgain() => AcquireCondition("Disease Duplicate", diseaseDefinition, PersistentConditionCategory.Disease);

        [ContextMenu("Batch 9/Conditions/Acquire Quirk")]
        public void AcquireQuirk()
        {
            if (!TryGetCondition("Quirk Acquire", quirkDefinition, PersistentConditionCategory.Quirk, out string reason))
            {
                ReportUnavailable("Quirk Acquire", reason);
                return;
            }

            PersistentConditionMutationStatus result = persistentConditions.TryAcquire(targetCharacterId, quirkDefinition);
            RefreshInspectorState();
            Report($"Quirk Acquire: '{currentQuirkId}' / Result={result}, Disease={HasDisease()}, Quirk={HasQuirk()}");
        }

        [ContextMenu("Batch 9/Conditions/Remove Disease")]
        public void RemoveDisease()
        {
            if (!TryGetCondition("Disease Remove", diseaseDefinition, PersistentConditionCategory.Disease, out string reason))
            {
                ReportUnavailable("Disease Remove", reason);
                return;
            }

            PersistentConditionMutationStatus result = persistentConditions.TryRemove(
                targetCharacterId, diseaseDefinition.ConditionId, PersistentConditionCategory.Disease);
            RefreshInspectorState();
            Report($"Disease Remove: '{currentDiseaseId}' / Result={result}, Disease={HasDisease()}, Quirk={HasQuirk()}");
        }

        [ContextMenu("Batch 9/Conditions/Print Conditions")]
        public void PrintConditions()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Conditions", reason); return; }
            RefreshInspectorState();
            Report($"Conditions: Character='{currentCharacterId}', Disease='{currentDiseaseId}' Present={HasDisease()}, Quirk='{currentQuirkId}' Present={HasQuirk()}");
        }

        [ContextMenu("Batch 9/Save Load/Save Current State")]
        public void SaveCurrentState()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Save", reason); return; }
            string state = FormatCurrentState();
            bool result = saveLoadService.TrySave(out string message);
            RefreshInspectorState();
            Report($"Save: {state} / Result={result}, Message='{message}'");
        }

        [ContextMenu("Batch 9/Save Load/Mutate State For Load Test")]
        public void MutateStateForLoadTest()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Load Test Mutation", reason); return; }
            string before = FormatCurrentState();
            bool shiningChanged = loadTestShiningChange != 0 && explorationResources.TryAddShining(loadTestShiningChange);
            bool hungerChanged = loadTestHungerChange != 0 && explorationResources.TryChangeHunger(loadTestHungerChange);
            PersistentConditionMutationStatus diseaseResult = RemoveForLoadTest(diseaseDefinition, PersistentConditionCategory.Disease);
            PersistentConditionMutationStatus quirkResult = RemoveForLoadTest(quirkDefinition, PersistentConditionCategory.Quirk);
            RefreshInspectorState();
            Report($"Load Test Mutation: Before[{before}] After[{FormatCurrentState()}] / ShiningChanged={shiningChanged}, HungerChanged={hungerChanged}, Disease={diseaseResult}, Quirk={quirkResult}");
        }

        [ContextMenu("Batch 9/Save Load/Load Saved State")]
        public void LoadSavedState()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Load", reason); return; }
            bool accepted = saveLoadService.TryLoad(out string message);
            RefreshInspectorState();
            Report($"Load: Accepted={accepted}, Message='{message}', State[{FormatCurrentState()}]");
        }

        [ContextMenu("Batch 9/Print Current State")]
        public void PrintCurrentState()
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable("Current State", reason); return; }
            RefreshInspectorState();
            Report($"Current State: {FormatCurrentState()}");
        }

        private void SpendShining(int amount)
        {
            if (!TryPrepare(out string reason)) { ReportUnavailable($"Shining Spend {amount}", reason); return; }
            int before = explorationResources.Shining;
            bool result = explorationResources.TrySpendShining(amount);
            RefreshInspectorState();
            Report($"Shining Spend {amount}: {before} -> {currentShining} / Result={result}");
        }

        private void AcquireCondition(string label, PersistentConditionDefinitionSO definition, PersistentConditionCategory expectedCategory)
        {
            if (!TryGetCondition(label, definition, expectedCategory, out string reason))
            {
                ReportUnavailable(label, reason);
                return;
            }

            PersistentConditionMutationStatus result = persistentConditions.TryAcquire(targetCharacterId, definition);
            RefreshInspectorState();
            Report($"{label}: '{definition.ConditionId}' / Result={result}, Present={persistentConditions.HasCondition(targetCharacterId, definition.ConditionId, expectedCategory)}");
        }

        private bool TryGetCondition(string label, PersistentConditionDefinitionSO definition, PersistentConditionCategory category, out string reason)
        {
            if (!TryPrepare(out reason)) return false;
            if (definition == null) { reason = $"Assign the {label} definition in the Inspector."; return false; }
            if (definition.Category != category) { reason = $"Definition '{definition.ConditionId}' is {definition.Category}, expected {category}."; return false; }
            if (string.IsNullOrWhiteSpace(definition.ConditionId)) { reason = "The assigned definition has no stable condition ID."; return false; }
            return true;
        }

        private bool TryPrepare(out string reason)
        {
            if (!Application.isPlaying) { reason = "Enter Play Mode before running Batch 9 commands."; return false; }
            ResolveServices();
            if (explorationResources == null || persistentConditions == null || feastService == null || inventoryService == null || saveLoadService == null)
            {
                reason = $"Missing service(s): Resources={explorationResources != null}, Conditions={persistentConditions != null}, Feast={feastService != null}, Inventory={inventoryService != null}, SaveLoad={saveLoadService != null}.";
                return false;
            }
            reason = null;
            return true;
        }

        private void ResolveServices()
        {
            explorationResources ??= ExplorationResourceRuntime.Instance;
            persistentConditions ??= PersistentConditionRuntime.Instance;
            feastService ??= FeastService.Instance;
            inventoryService ??= InventoryService.Instance;
            saveLoadService ??= SaveLoadService.Instance;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribedResources != explorationResources)
            {
                if (_subscribedResources != null) { _subscribedResources.Changed -= OnResourceChanged; _subscribedResources.Refreshed -= RefreshInspectorState; }
                _subscribedResources = explorationResources;
                if (_subscribedResources != null) { _subscribedResources.Changed += OnResourceChanged; _subscribedResources.Refreshed += RefreshInspectorState; }
            }
            if (_subscribedConditions != persistentConditions)
            {
                if (_subscribedConditions != null) { _subscribedConditions.Changed -= OnConditionChanged; _subscribedConditions.Refreshed -= RefreshInspectorState; }
                _subscribedConditions = persistentConditions;
                if (_subscribedConditions != null) { _subscribedConditions.Changed += OnConditionChanged; _subscribedConditions.Refreshed += RefreshInspectorState; }
            }
            if (_subscribedInventory != inventoryService)
            {
                if (_subscribedInventory != null) { _subscribedInventory.Changed -= OnInventoryChanged; _subscribedInventory.Refreshed -= RefreshInspectorState; }
                _subscribedInventory = inventoryService;
                if (_subscribedInventory != null) { _subscribedInventory.Changed += OnInventoryChanged; _subscribedInventory.Refreshed += RefreshInspectorState; }
            }
            if (_subscribedSaveLoad != saveLoadService)
            {
                if (_subscribedSaveLoad != null) _subscribedSaveLoad.OnLoadCompleted -= OnLoadCompleted;
                _subscribedSaveLoad = saveLoadService;
                if (_subscribedSaveLoad != null) _subscribedSaveLoad.OnLoadCompleted += OnLoadCompleted;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedResources != null) { _subscribedResources.Changed -= OnResourceChanged; _subscribedResources.Refreshed -= RefreshInspectorState; }
            if (_subscribedConditions != null) { _subscribedConditions.Changed -= OnConditionChanged; _subscribedConditions.Refreshed -= RefreshInspectorState; }
            if (_subscribedInventory != null) { _subscribedInventory.Changed -= OnInventoryChanged; _subscribedInventory.Refreshed -= RefreshInspectorState; }
            if (_subscribedSaveLoad != null) _subscribedSaveLoad.OnLoadCompleted -= OnLoadCompleted;
            _subscribedResources = null;
            _subscribedConditions = null;
            _subscribedInventory = null;
            _subscribedSaveLoad = null;
        }

        private void OnResourceChanged(ExplorationResourceChange _) => RefreshInspectorState();
        private void OnConditionChanged(PersistentConditionChange _) => RefreshInspectorState();
        private void OnInventoryChanged(InventoryMutationResult _) => RefreshInspectorState();

        private void OnLoadCompleted(bool succeeded, string message)
        {
            RefreshInspectorState();
            Report($"Load Completed: Result={succeeded}, Message='{message}', State[{FormatCurrentState()}]");
        }

        private void RefreshInspectorState()
        {
            currentShining = explorationResources != null ? explorationResources.Shining : 0;
            currentHunger = explorationResources != null ? explorationResources.Hunger : 0;
            currentCharacterId = targetCharacterId;
            currentDiseaseId = diseaseDefinition != null ? diseaseDefinition.ConditionId : string.Empty;
            currentQuirkId = quirkDefinition != null ? quirkDefinition.ConditionId : string.Empty;
            currentFeastItemId = feastItemId;
            currentFeastItemCount = inventoryService != null ? inventoryService.GetCount(feastItemId) : 0;
            currentHungerRestoreAmount = hungerRestoreAmount;
        }

        private PersistentConditionMutationStatus RemoveForLoadTest(PersistentConditionDefinitionSO definition, PersistentConditionCategory category)
        {
            if (definition == null || definition.Category != category || string.IsNullOrWhiteSpace(definition.ConditionId))
                return PersistentConditionMutationStatus.InvalidConditionId;
            return persistentConditions.TryRemove(targetCharacterId, definition.ConditionId, category);
        }

        private bool HasDisease() => HasCondition(diseaseDefinition, PersistentConditionCategory.Disease);
        private bool HasQuirk() => HasCondition(quirkDefinition, PersistentConditionCategory.Quirk);

        private bool HasCondition(PersistentConditionDefinitionSO definition, PersistentConditionCategory category) =>
            persistentConditions != null && definition != null &&
            persistentConditions.HasCondition(targetCharacterId, definition.ConditionId, category);

        private string FormatCurrentState() =>
            $"Shining={currentShining}, Hunger={currentHunger}, Item='{currentFeastItemId}' Count={currentFeastItemCount}, Disease={HasDisease()}, Quirk={HasQuirk()}";

        private void Report(string message)
        {
            lastResult = message;
            Debug.Log($"{LogPrefix} {message}", this);
        }

        private void ReportUnavailable(string operation, string reason)
        {
            lastResult = $"{operation}: Unavailable - {reason}";
            Debug.LogWarning($"{LogPrefix} {lastResult}", this);
        }
    }
}
