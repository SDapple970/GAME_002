using System.Collections.Generic;
using Game.NonCombat.Inventory;
using UnityEngine;

namespace Game.Shop
{
    public sealed class ShopService : MonoBehaviour
    {
        [SerializeField] private ShopInventorySO shopInventory;
        [SerializeField] private List<ShopItemEntry> localItems = new();
        [SerializeField] private PriceRule priceRule;
        [SerializeField] private CurrencyWallet currencyWallet;
        [SerializeField] private InventoryService inventoryService;

        private bool _missingCurrencyWalletWarned;
        private bool _missingInventoryServiceWarned;
        private bool _insufficientCurrencyWarned;
        private bool _invalidShopItemWarned;

        public IReadOnlyList<ShopItemEntry> GetAvailableEntries(bool includeLocked = false)
        {
            List<ShopItemEntry> available = new();
            AppendEntries(available, shopInventory != null ? shopInventory.Items : null, includeLocked);
            AppendEntries(available, localItems, includeLocked);
            return available;
        }

        public ShopPurchaseResult Purchase(ShopPurchaseRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.itemId))
            {
                WarnInvalidShopItem(request != null ? request.itemId : null);
                return ShopPurchaseResult.Failed(request != null ? request.itemId : null, "Invalid shop item id.");
            }

            ShopItemEntry entry = FindEntry(request.itemId);
            if (entry == null)
            {
                WarnInvalidShopItem(request.itemId);
                return ShopPurchaseResult.Failed(request.itemId, "Shop item was not found or is locked.");
            }

            ResolveReferences();
            if (inventoryService == null)
            {
                WarnMissingInventoryService();
                return ShopPurchaseResult.Failed(request.itemId, "InventoryService is missing.");
            }

            int requestedQuantity = Mathf.Max(1, request.quantity);
            int grantedQuantity = Mathf.Max(1, entry.Quantity) * requestedQuantity;
            int unitPrice = priceRule != null ? priceRule.Apply(entry.Price) : entry.Price;
            int totalPrice = Mathf.Max(0, unitPrice * requestedQuantity);

            if (totalPrice > 0)
            {
                if (currencyWallet == null)
                {
                    WarnMissingCurrencyWallet();
                    return ShopPurchaseResult.Failed(request.itemId, "CurrencyWallet is missing.");
                }

                if (!currencyWallet.TrySpendGold(totalPrice))
                {
                    WarnInsufficientCurrency(request.itemId, totalPrice);
                    return ShopPurchaseResult.Failed(request.itemId, "Insufficient currency.");
                }
            }

            inventoryService.AddItem(entry.ItemId, grantedQuantity);
            return new ShopPurchaseResult
            {
                success = true,
                itemId = entry.ItemId,
                displayName = entry.DisplayName,
                purchasedQuantity = grantedQuantity,
                unitPrice = unitPrice,
                totalPrice = totalPrice
            };
        }

        private ShopItemEntry FindEntry(string itemId)
        {
            IReadOnlyList<ShopItemEntry> entries = GetAvailableEntries(false);
            for (int i = 0; i < entries.Count; i++)
            {
                ShopItemEntry entry = entries[i];
                if (entry != null && entry.ItemId == itemId)
                    return entry;
            }

            return null;
        }

        private void ResolveReferences()
        {
            if (currencyWallet == null)
                currencyWallet = CurrencyWallet.Instance != null
                    ? CurrencyWallet.Instance
                    : FindFirstObjectByType<CurrencyWallet>();

            if (inventoryService == null)
                inventoryService = InventoryService.Instance != null
                    ? InventoryService.Instance
                    : FindFirstObjectByType<InventoryService>();
        }

        private static void AppendEntries(List<ShopItemEntry> target, IReadOnlyList<ShopItemEntry> source, bool includeLocked)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                ShopItemEntry entry = source[i];
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                if (!includeLocked && !entry.Unlocked)
                    continue;

                target.Add(entry);
            }
        }

        private void WarnMissingCurrencyWallet()
        {
            if (_missingCurrencyWalletWarned)
                return;

            _missingCurrencyWalletWarned = true;
            Debug.LogWarning("[ShopService] CurrencyWallet is missing. Purchase cannot be completed.", this);
        }

        private void WarnMissingInventoryService()
        {
            if (_missingInventoryServiceWarned)
                return;

            _missingInventoryServiceWarned = true;
            Debug.LogWarning("[ShopService] InventoryService is missing. Purchase cannot be completed.", this);
        }

        private void WarnInsufficientCurrency(string itemId, int totalPrice)
        {
            if (_insufficientCurrencyWarned)
                return;

            _insufficientCurrencyWarned = true;
            Debug.LogWarning($"[ShopService] Insufficient currency for purchase. itemId={itemId}, totalPrice={totalPrice}", this);
        }

        private void WarnInvalidShopItem(string itemId)
        {
            if (_invalidShopItemWarned)
                return;

            _invalidShopItemWarned = true;
            Debug.LogWarning($"[ShopService] Invalid shop item id. itemId={itemId}", this);
        }
    }
}
