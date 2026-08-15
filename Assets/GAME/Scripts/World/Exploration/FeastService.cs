using Game.NonCombat.Inventory;
using UnityEngine;

namespace Game.World.Exploration
{
    public enum FeastStatus
    {
        Success = 0,
        InvalidRequest = 10,
        MissingService = 20,
        InsufficientItems = 30,
        HungerChangeRejected = 40,
        InventoryMutationFailed = 50
    }

    public readonly struct FeastRequest
    {
        public FeastRequest(string itemId, int itemCount, int hungerRestoration)
        {
            ItemId = itemId; ItemCount = itemCount; HungerRestoration = hungerRestoration;
        }
        public string ItemId { get; }
        public int ItemCount { get; }
        public int HungerRestoration { get; }
    }

    public readonly struct FeastResult
    {
        public FeastResult(FeastStatus status) => Status = status;
        public FeastStatus Status { get; }
        public bool Succeeded => Status == FeastStatus.Success;
    }

    public sealed class FeastService : MonoBehaviour
    {
        public static FeastService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public FeastResult TryFeast(FeastRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ItemId) || request.ItemCount <= 0 || request.HungerRestoration <= 0)
                return new FeastResult(FeastStatus.InvalidRequest);

            InventoryService inventory = InventoryService.Instance;
            ExplorationResourceRuntime resources = ExplorationResourceRuntime.Instance;
            if (inventory == null || resources == null) return new FeastResult(FeastStatus.MissingService);
            if (inventory.GetCount(request.ItemId) < request.ItemCount) return new FeastResult(FeastStatus.InsufficientItems);
            if (!resources.CanChangeHunger(request.HungerRestoration)) return new FeastResult(FeastStatus.HungerChangeRejected);

            InventoryMutationResult removal = inventory.TryRemoveItemDetailed(request.ItemId, request.ItemCount);
            if (removal.Status != InventoryMutationStatus.Success) return new FeastResult(FeastStatus.InventoryMutationFailed);
            if (resources.TryChangeHunger(request.HungerRestoration)) return new FeastResult(FeastStatus.Success);

            inventory.TryAddItem(request.ItemId, request.ItemCount);
            return new FeastResult(FeastStatus.HungerChangeRejected);
        }
    }
}
